using System;
using System.Linq;
using System.Threading.Tasks;
using Hangfire.Dashboard.Resources;
using In.ProjectEKA.HipService.UserAuth.Database;
using In.ProjectEKA.HipService.UserAuth.Model;
using Microsoft.EntityFrameworkCore;
using Optional;
using Serilog;

namespace In.ProjectEKA.HipService.UserAuth
{
    public class UserAuthRepository : IUserAuthRepository
    {
        private readonly AuthContext authContext;
        private readonly NdhmDemographicsContext ndhmDemographicsContext;

        public UserAuthRepository(AuthContext authContext, NdhmDemographicsContext ndhmDemographicsContext)
        {
            this.authContext = authContext;
            this.ndhmDemographicsContext = ndhmDemographicsContext;
        }

        public async Task<Option<AuthConfirm>> Get(string healthId, string hipId)
        {
            var healthIdKey = healthId.ToLowerInvariant();
            var hipIdKey = hipId.ToLowerInvariant();
            var authConfirm = await authContext.AuthConfirm
                .FirstOrDefaultAsync(c =>
                    c.HealthId.ToLower() == healthIdKey && c.HipId.ToLower() == hipIdKey).ConfigureAwait(false);
            // if (authConfirm == null && !string.IsNullOrEmpty(hipId))
            // {
            //     authConfirm = await authContext.AuthConfirm
            //         .FirstOrDefaultAsync(c =>
            //             c.HealthId == healthId && c.HipId == string.Empty).ConfigureAwait(false);
            // }
            if (authConfirm != null)
                authContext.Entry<AuthConfirm>(authConfirm).State = EntityState.Detached;
            return Option.Some<AuthConfirm>(authConfirm);
        }

        public async Task<Option<NdhmDemographics>> GetDemographics(string healthId)
        {
            healthId = healthId.ToLowerInvariant();
            var ndhmDemographics = await ndhmDemographicsContext.NdhmDemographics
                .FirstOrDefaultAsync(c =>
                    c.HealthId.ToLower() == healthId).ConfigureAwait(false);
            return Option.Some<NdhmDemographics>(ndhmDemographics);
        }

        public async Task<string> GetHealthIdByPhoneNumber(string phoneNumber)
        {
            var normalized = UserAuthMap.NormalizePhoneNumber(phoneNumber);
            if (string.IsNullOrEmpty(normalized))
                return null;

            var normalizedKey = normalized.ToLowerInvariant();
            var candidates = new[]
            {
                phoneNumber.Trim(),
                normalized,
                "91" + normalized,
                "+91" + normalized,
                "0" + normalized
            }.Where(value => !string.IsNullOrEmpty(value))
                .Select(value => value.ToLowerInvariant())
                .Distinct()
                .ToArray();

            var matches = await ndhmDemographicsContext.NdhmDemographics
                .Where(d => d.PhoneNumber != null &&
                            (candidates.Contains(d.PhoneNumber.ToLower()) ||
                             d.PhoneNumber.ToLower().EndsWith(normalizedKey)))
                .ToListAsync()
                .ConfigureAwait(false);
            if (matches.Count == 0)
                return null;

            var exact = matches.FirstOrDefault(d =>
                d.PhoneNumber != null && candidates.Contains(d.PhoneNumber.ToLower()));
            return (exact ?? matches[0]).HealthId;
        }

        public async Task<Option<AuthConfirm>> Add(AuthConfirm authConfirm)
        {
            try
            {
                await authContext.AuthConfirm.AddAsync(authConfirm).ConfigureAwait(false);
                await authContext.SaveChangesAsync();
                authContext.Entry<AuthConfirm>(authConfirm).State = EntityState.Detached;
                return Option.Some(authConfirm);
            }
            catch (Exception e)
            {
                Log.Fatal(e, e.StackTrace);
                return Option.None<AuthConfirm>();
            }
        }

        public async Task<Option<NdhmDemographics>> AddDemographics(NdhmDemographics ndhmDemographics)
        {
            try
            {
                var healthIdKey = ndhmDemographics.HealthId.ToLowerInvariant();
                var result = await ndhmDemographicsContext.NdhmDemographics
                    .FirstOrDefaultAsync(c =>
                        c.HealthId.ToLower() == healthIdKey).ConfigureAwait(false);
                if (result != null) return Option.None<NdhmDemographics>();
                await ndhmDemographicsContext.NdhmDemographics.AddAsync(ndhmDemographics).ConfigureAwait(false);
                await ndhmDemographicsContext.SaveChangesAsync();
                ndhmDemographicsContext.Entry(ndhmDemographics).State = EntityState.Detached;
                return Option.Some<NdhmDemographics>(ndhmDemographics);

            }
            catch (Exception e)
            {
                Log.Fatal(e, e.StackTrace);
                return Option.None<NdhmDemographics>();
            }
        }

        public bool Update(AuthConfirm authConfirm)
        {
            try
            {
                DetachTracked(authConfirm.HealthId, authConfirm.HipId);
                authContext.AuthConfirm.Update(authConfirm);
                authContext.SaveChanges();
                authContext.Entry(authConfirm).State = EntityState.Detached;
                return true;
            }
            catch (Exception e)
            {
                Log.Fatal(e, e.StackTrace);
                return false;
            }
        }

        private void DetachTracked(string healthId, string hipId)
        {
            var healthIdKey = healthId?.ToLowerInvariant();
            var hipIdKey = hipId?.ToLowerInvariant();
            var tracked = authContext.ChangeTracker.Entries<AuthConfirm>()
                .Where(entry =>
                    (entry.Entity.HealthId == null ? null : entry.Entity.HealthId.ToLower()) == healthIdKey &&
                    (entry.Entity.HipId == null ? null : entry.Entity.HipId.ToLower()) == hipIdKey)
                .ToList();
            foreach (var entry in tracked)
                entry.State = EntityState.Detached;
        }

        public async Task Delete(string healthId, string hipId = null)
        {
            var healthIdKey = healthId.ToLowerInvariant();
            var query = authContext.AuthConfirm.Where(request => request.HealthId.ToLower() == healthIdKey);
            if (hipId != null)
            {
                var hipIdKey = hipId.ToLowerInvariant();
                query = query.Where(request => request.HipId.ToLower() == hipIdKey);
            }
            var deleteRequests = await query.ToListAsync();
            authContext.RemoveRange(deleteRequests);
            await authContext.SaveChangesAsync();
        }

        public async Task DeleteDemographics(string healthId)
        {
            var healthIdKey = healthId.ToLowerInvariant();
            var deleteRequest = await ndhmDemographicsContext.NdhmDemographics
                .FirstAsync(request =>
                    request.HealthId.ToLower() == healthIdKey);
            ndhmDemographicsContext.Remove(deleteRequest);
            await ndhmDemographicsContext.SaveChangesAsync();
        }

        public async Task<Tuple<string, Exception>> GetAccessToken(
            string healthId, string hipId)
        {
            try
            {
                AuthConfirm authRequest = null;
                if (!string.IsNullOrEmpty(healthId) && !string.IsNullOrEmpty(hipId))
                {
                    var healthIdKey = healthId.ToLowerInvariant();
                    var hipIdKey = hipId.ToLowerInvariant();
                    authRequest = await authContext.AuthConfirm
                        .FirstOrDefaultAsync(request =>
                            request.HealthId.ToLower() == healthIdKey &&
                            request.HipId.ToLower() == hipIdKey).ConfigureAwait(false);
                // if (authRequest == null && !string.IsNullOrEmpty(hipId))
                // {
                //     authRequest = await authContext.AuthConfirm
                //         .FirstOrDefaultAsync(request =>
                //             request.HealthId.Equals(healthId) && request.HipId.Equals(string.Empty));
                // }
                }
                if (authRequest != null)
                    authContext.Entry(authRequest).State = EntityState.Detached;
                return new Tuple<string, Exception>(authRequest != null ? authRequest.AccessToken : null, null);
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, exception.StackTrace);
                return new Tuple<string, Exception>(null, exception);
            }
        }

        public async Task<Tuple<AuthConfirm, Exception>> GetAuthDetailsByHealthId(string healthId, string hipId = null)
        {
            try
            {
                AuthConfirm authRequest = null;
                if (string.IsNullOrEmpty(healthId))
                    return new Tuple<AuthConfirm, Exception>(null, null);

                var healthIdKey = healthId.ToLowerInvariant();
                if (!string.IsNullOrEmpty(hipId))
                {
                    var hipIdKey = hipId.ToLowerInvariant();
                    authRequest = await authContext.AuthConfirm
                        .Where(request => request.HealthId.ToLower() == healthIdKey
                                        && request.HipId != null
                                        && request.HipId != ""
                                        && request.HipId.ToLower() == hipIdKey)
                        .FirstOrDefaultAsync()
                        .ConfigureAwait(false);
                }
                else
                {
                    authRequest = await authContext.AuthConfirm
                        .Where(request => request.HealthId.ToLower() == healthIdKey)
                        .FirstOrDefaultAsync()
                        .ConfigureAwait(false);
                }
                if (authRequest != null)
                    authContext.Entry(authRequest).State = EntityState.Detached;
                return new Tuple<AuthConfirm, Exception>(authRequest, null);
            }
            catch (Exception exception)
            {
                Log.Error(exception, exception.StackTrace);
                return new Tuple<AuthConfirm, Exception>(null, exception);
            }
        }
    }
}