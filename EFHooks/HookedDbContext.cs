﻿using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;

namespace EFHooks
{
    public abstract class HookedDbContext : DbContext
    {
        protected IList<IPreActionHook> PreHooks;
        protected IList<IPostActionHook> PostHooks;

        public HookedDbContext()
        {
            PreHooks = new List<IPreActionHook>();
            PostHooks = new List<IPostActionHook>();
        }

        public HookedDbContext(IHook[] hooks)
        {
            PreHooks = hooks.OfType<IPreActionHook>().ToList();
            PostHooks = hooks.OfType<IPostActionHook>().ToList();
        }

        public void RegisterHook(IPreActionHook hook)
        {
            this.PreHooks.Add(hook);
        }

        public override int SaveChanges()
        {
            bool hasValidationErrors = this.Configuration.ValidateOnSaveEnabled && this.ChangeTracker.Entries().Any(x => !x.GetValidationResult().IsValid);
            bool hasPostHooks = this.PostHooks.Any(); // save this to a local variable since we're checking this again later.

            var modifiedEntries = hasValidationErrors && !hasPostHooks
                                      ? new DbEntityEntry[0]
                                      : this.ChangeTracker.Entries()
                                            .Where(x => x.State != EntityState.Unchanged && x.State != EntityState.Detached)
                                            .ToArray();

            if (!hasValidationErrors)
            {
                foreach (var entityEntry in modifiedEntries)
                {
                    foreach (var hook in PreHooks)
                    {
                        var metadata = new HookEntityMetadata(entityEntry.State);
                        hook.HookObject(entityEntry.Entity, metadata);

                        if (metadata.HasStateChanged)
                        {
                            entityEntry.State = metadata.State;
                        }
                    }
                }
            }

            int result = base.SaveChanges();

            if (hasPostHooks)
            {
                foreach (var entityEntry in modifiedEntries)
                {
                    foreach (var hook in PostHooks)
                    {
                        var metadata = new HookEntityMetadata(entityEntry.State);
                        hook.HookObject(entityEntry.Entity, metadata);
                    }
                }
            }

            return result;
        }
    }
}