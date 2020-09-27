﻿using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace MongoDB.Entities
{
    public static partial class DB
    {
        private static async Task<DeleteResult> DeleteCascadingAsync<T>(IEnumerable<string> IDs, IClientSessionHandle session = null) where T : IEntity
        {
            // note: cancellation should not be enabled because multiple collections are involved 
            //       and premature cancellation could cause data inconsistencies.

            var db = Database<T>();
            var options = new ListCollectionNamesOptions
            {
                Filter = "{$and:[{name:/~/},{name:/" + CollectionName<T>() + "/}]}"
            };

            var tasks = new HashSet<Task>();

            foreach (var cName in await db.ListCollectionNames(options).ToListAsync().ConfigureAwait(false))
            {
                tasks.Add(
                    session == null
                    ? db.GetCollection<JoinRecord>(cName).DeleteManyAsync(r => IDs.Contains(r.ChildID) || IDs.Contains(r.ParentID))
                    : db.GetCollection<JoinRecord>(cName).DeleteManyAsync(session, r => IDs.Contains(r.ChildID) || IDs.Contains(r.ParentID), null));
            }

            var delResTask =
                    session == null
                    ? Collection<T>().DeleteManyAsync(x => IDs.Contains(x.ID))
                    : Collection<T>().DeleteManyAsync(session, x => IDs.Contains(x.ID), null);

            tasks.Add(delResTask);

            if (typeof(T).BaseType == typeof(FileEntity))
            {
                tasks.Add(
                    session == null
                    ? db.GetCollection<FileChunk>(CollectionName<FileChunk>()).DeleteManyAsync(x => IDs.Contains(x.FileID))
                    : db.GetCollection<FileChunk>(CollectionName<FileChunk>()).DeleteManyAsync(session, x => IDs.Contains(x.FileID), null));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            return await delResTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a single entity from MongoDB.
        /// <para>HINT: If this entity is referenced by one-to-many/many-to-many relationships, those references are also deleted.</para>
        /// </summary>
        /// <typeparam name="T">Any class that implements IEntity</typeparam>
        /// <param name="ID">The Id of the entity to delete</param>
        /// <param name = "session" > An optional session if using within a transaction</param>
        public static Task<DeleteResult> DeleteAsync<T>(string ID, IClientSessionHandle session = null) where T : IEntity
        {
            return DeleteCascadingAsync<T>(new[] { ID }, session);
        }

        /// <summary>
        /// Deletes matching entities from MongoDB
        /// <para>HINT: If the expression matches more than 250,000 entities, they will be deleted in batches of 250k.</para>
        /// <para>HINT: If these entities are referenced by one-to-many/many-to-many relationships, those references are also deleted.</para>
        /// </summary>
        /// <typeparam name="T">Any class that implements IEntity</typeparam>
        /// <param name="expression">A lambda expression for matching entities to delete.</param>
        /// <param name = "session" >An optional session if using within a transaction</param>
        public static async Task<DeleteResult> DeleteAsync<T>(Expression<Func<T, bool>> expression, IClientSessionHandle session = null) where T : IEntity
        {
            long deleteCount = 0;

            using (var cursor = await new Find<T, string>(session).Match(expression).Project(e => e.ID).Option(o => o.BatchSize = 250000).ExecuteCursorAsync().ConfigureAwait(false))
            {
                while (await cursor.MoveNextAsync().ConfigureAwait(false))
                {
                    if (cursor.Current.Any())
                        deleteCount += (await DeleteCascadingAsync<T>(cursor.Current).ConfigureAwait(false)).DeletedCount;
                }
            }

            return new DeleteResult.Acknowledged(deleteCount);
            //todo: write test for deleting 1mil entities.
        }

        /// <summary>
        /// Deletes matching entities from MongoDB
        /// <para>HINT: If these entities are referenced by one-to-many/many-to-many relationships, those references are also deleted.</para>
        /// <para>TIP: Try to keep the number of entities to delete under 100 in a single call</para>
        /// </summary>
        /// <typeparam name="T">Any class that implements IEntity</typeparam>
        /// <param name="IDs">An IEnumerable of entity IDs</param>
        /// <param name = "session" > An optional session if using within a transaction</param>
        public static Task<DeleteResult> DeleteAsync<T>(IEnumerable<string> IDs, IClientSessionHandle session = null) where T : IEntity
        {
            return DeleteCascadingAsync<T>(IDs, session);
        }
    }
}
