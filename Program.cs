/*
MIT License

Copyright (c) 2020 PriceRunner

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text;
using System.IO;
using Model;
using System.Linq;


namespace PriceRunner_API_Client
{
	class Program
	{
		private static readonly HttpClient client = new HttpClient();

		static async Task Main(string[] args)
		{
			if(args.Length < 3)
			{
				Console.WriteLine("Usage Program [TOKEN_ID] [SKU_FILE_PATH] [RESULT_FILE_PATH]");
				Environment.Exit(0);
			}
			Program p = new Program();
			await p.ProcessSkus(args[0], args[1], args[2]);
		}

		private async Task ProcessSkus(string token, string skuPath, string resultPath)
		{
			var lines = File.ReadAllLines(skuPath);
			var skuList = new List<string>(lines);
			StringBuilder resultFileContents = new StringBuilder();
			//At most 100 skus per request
			foreach (var batch in skuList.Batch(100))
			{
				List<String> batchOfSkus = new List<String>(batch);
				var skus = new MerchantSkus(batchOfSkus);
				var json = JsonConvert.SerializeObject(skus);
				var data = new StringContent(json, Encoding.UTF8, "application/json");

				client.DefaultRequestHeaders.Accept.Clear();
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
				client.DefaultRequestHeaders.Add("tokenId", token);

				string url = "https://api.pricerunner.com/public/v1/merchant/product/offers/sku";
				var response = await client.PostAsync(url, data);
				string result = response.Content.ReadAsStringAsync().Result;

				ProductListingsV1 productListings = (ProductListingsV1)JsonConvert.DeserializeObject<ProductListingsV1>(result);
				foreach (ProductListingV1 productListing in productListings.ProductListings)
				{
					resultFileContents.Append(productListing.Product.Name + ";");
					foreach (OfferV1 offer in productListing.Offers)
					{
						if(offer.MerchantSku != null && batchOfSkus.Contains(offer.MerchantSku))
						{
							resultFileContents.Append(offer.MerchantSku + ";");
						}
					}

					foreach (OfferV1 offer in productListing.Offers)
					{
						resultFileContents.Append(String.Join(";", new String[] { offer.MerchantName, offer.OfferName, offer.Price.Value, offer.Price.Currency }) + ";");
					}
					resultFileContents.AppendLine();
				}
				System.IO.File.WriteAllText(@resultPath, resultFileContents.ToString());
			}
		}
	}
}
