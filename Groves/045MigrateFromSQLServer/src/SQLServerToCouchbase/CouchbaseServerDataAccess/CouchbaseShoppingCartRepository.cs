﻿using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase;
using Couchbase.Core;
using Couchbase.Linq;
using Couchbase.Linq.Extensions;
using Couchbase.N1QL;
using RestSharp;
using SQLServerToCouchbase.Core;
using SQLServerToCouchbase.Core.Shopping;

namespace CouchbaseServerDataAccess
{
    public class CouchbaseShoppingCartRepository : IShoppingCartRepository
    {
        private readonly IBucket _bucket;
        private readonly BucketContext _context;
        private readonly string _baseUrl;

        public CouchbaseShoppingCartRepository()
        {
            _bucket = ClusterHelper.GetBucket("sqltocb");
            _context = new BucketContext(_bucket);

            // the base URL for REST endpoints
            // typically this would NOT be the same base URL as the current web site
            // so this is just to keep the sample simple
            _baseUrl = System.Web.HttpContext.Current.Request.Url.GetComponents(UriComponents.SchemeAndServer | UriComponents.UserInfo, UriFormat.Unescaped);
        }

        public List<ShoppingCart> GetTenLatestShoppingCarts()
        {
            // this code uses the standard Couchbase .NET SDK
            /*
            var n1ql = @"SELECT META(c).id, c.*
                FROM `sqltocb` c
                WHERE c.type = 'ShoppingCart'
                ORDER BY STR_TO_MILLIS(c.dateCreated) DESC
                LIMIT 10;";
            var query = QueryRequest.Create(n1ql);
            query.ScanConsistency(ScanConsistency.RequestPlus);
            return _bucket.Query<ShoppingCart>(query).Rows;
            */

            // this code uses Linq2Couchbase
            // tag::Linq2CouchbaseExample[]
            var query = from c in _context.Query<ShoppingCart>()
                where c.Type == "ShoppingCart"  // could use DocumentFilter attribute instead of this Where
                orderby c.DateCreated descending
                select new {Cart = c, Id = N1QlFunctions.Meta(c).Id};
            var results = query.ScanConsistency(ScanConsistency.RequestPlus)
                .Take(10)
                .ToList();
            // end::Linq2CouchbaseExample[]
            results.ForEach(r => r.Cart.Id = Guid.Parse(r.Id));
            return results.Select(r => r.Cart).ToList();
        }

        public void SeedEmptyShoppingCart()
        {
            _bucket.Insert(new Document<dynamic>
            {
                Id = Guid.NewGuid().ToString(),
                Content = new
                {
                    User = Faker.Name.First().ToLower()[0] + Faker.Name.Last().ToLower(), // format first initial + last name, e.g. "mgroves"
                    DateCreated = DateTime.Now,
                    Items = new List<Item>(),
                    Type = "ShoppingCart"
                }
            });
        }

        // tag::GetCartById[]
        public ShoppingCart GetCartById(Guid id)
        {
            return _bucket.Get<ShoppingCart>(id.ToString()).Value;
        }
        // end::GetCartById[]

        public void AddItemToCart(Guid cartId, Item item)
        {
            // note that since I'm using the Item class
            // which is also being used for SQL in this demo
            // that there will be an "Id" field serialized to Couchbase
            // However, this Id field is completely unnecessary for Couchbase
            // and will always be '0' when in couchbase
            _bucket.MutateIn<ShoppingCart>(cartId.ToString())
                .ArrayAppend("items", item)
                .Execute();
        }

        // tag::SearchForCartsByUserName[]
        public List<ShoppingCart> SearchForCartsByUserName(string searchString)
        {
            // typically there would be authentication/authorization with a REST call like this
            var client = new RestClient(_baseUrl);
            var request = new RestRequest("/api/searchByName/" + searchString);
            request.AddHeader("Accept", "application/json");
            var response = client.Execute<List<ShoppingCart>>(request);
            return response.Data;
        }
        // end::SearchForCartsByUserName[]
    }
}