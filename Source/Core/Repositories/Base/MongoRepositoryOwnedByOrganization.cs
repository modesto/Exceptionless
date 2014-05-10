﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Models;
using MongoDB.Driver;

namespace Exceptionless.Core.Repositories {
    public class MongoRepositoryOwnedByOrganization<T> : MongoRepository<T>, IRepositoryOwnedByOrganization<T> where T : class, IOwnedByOrganization, IIdentity, new() {
        public MongoRepositoryOwnedByOrganization(MongoDatabase database, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(database, cacheClient, messagePublisher) {}

        protected override void BeforeAdd(IList<T> documents) {
            if (documents.Any(d => String.IsNullOrEmpty(d.OrganizationId)))
                throw new ArgumentException("OrganizationIds must be set.");

            base.BeforeAdd(documents);
        }

        public IList<T> GetByOrganizationId(string organizationId, bool useCache = false, TimeSpan? expiresIn = null) {
            return Find<T>(new FindMultipleOptions().WithOrganizationId(organizationId).WithCacheKey(useCache ? String.Concat("org:", organizationId) : null).WithExpiresIn(expiresIn));
        }

        public IList<T> GetByOrganizationId(IList<string> organizationIds, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return new List<T>();

            string cacheKey = String.Concat("org:", String.Join("", organizationIds).GetHashCode().ToString());
            return Find<T>(new FindMultipleOptions().WithOrganizationIds(organizationIds).WithCacheKey(useCache ? cacheKey : null).WithExpiresIn(expiresIn));
        }

        public void RemoveAllByOrganizationId(string organizationId) {
            RemoveAll(new QueryOptions().WithOrganizationId(organizationId));
        }

        public async Task RemoveAllByOrganizationIdAsync(string organizationId) {
            await Task.Run(() => RemoveAllByOrganizationId(organizationId));
        }
    }
}