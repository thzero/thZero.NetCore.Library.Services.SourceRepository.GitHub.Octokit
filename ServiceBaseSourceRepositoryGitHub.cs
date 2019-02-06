/* ------------------------------------------------------------------------- *
thZero.NetCore.Library.Services.CircuitBreaker.Steeltoe
Copyright (C) 2016-2018 thZero.com

<development [at] thzero [dot] com>

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

	http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
 * ------------------------------------------------------------------------- */

using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Nito.AsyncEx;

using Octokit;

using thZero.Configuration;

namespace thZero.Services
{
    public sealed class ServiceBaseSourceRepositoryGithubOctokit : ServiceBaseSourceRepository<ServiceBaseSourceRepositoryGithubOctokit>
    {
        public ServiceBaseSourceRepositoryGithubOctokit(IOptions<SourceRepository> config, ILogger<ServiceBaseSourceRepositoryGithubOctokit> logger) : base(config, logger)
        {
        }

        #region Public Methods
        public async override Task<SourceRepositoryProfile> GetProfile()
        {
            if (RefreshRequired())
                return _profile;

            using (await _mutex.LockAsync())
            {
                if (RefreshRequired())
                    return _profile;

                var client = new GitHubClient(new ProductHeaderValue("thzero"));

                if (Config == null)
                    throw new Exception("Invalid Configuration.");

                if (!string.IsNullOrEmpty(Config.User) && !string.IsNullOrEmpty(Config.Password))
                {
                    var basicAuth = new Credentials(Config.User, Config.Password);
                    client.Credentials = basicAuth;
                }
                else if (!string.IsNullOrEmpty(Config.Token))
                {
                    var tokenAuth = new Credentials(Config.Token); // NOTE: not real token
                    client.Credentials = tokenAuth;
                }
                else
                    throw new Exception("Invalid Configuration; no token or user/password.");

                if (string.IsNullOrEmpty(Config.User) && string.IsNullOrEmpty(Config.Profile))
                    throw new Exception("Invalid Configurationl missing profile.");

                SourceRepositoryProfile results = new SourceRepositoryProfile();

                string profileName = !string.IsNullOrEmpty(Config.User) ? Config.User : Config.Profile;
                if (string.IsNullOrEmpty(profileName))
                    throw new Exception("Invalid Configurationl missing profile name.");

                var profile = await client.User.Get(profileName);
                results.Name = profile.Name;
                results.Url = profile.HtmlUrl;

                var repos = await client.Repository.GetAllForUser(profileName);
                foreach (var repo in repos)
                {
                    results.Repos.Add(new SourceRepositoryProfileRepo() {
                        Description = repo.Description,
                        FullName = repo.FullName,
                        Language = repo.Language,
                        Name = repo.Name,
                        UpdatedAt = repo.UpdatedAt,
                        Url = repo.HtmlUrl });

                    _last = DateTime.Now.Ticks;
                    _profile = results;
                }
            }

            return _profile;
        }
        #endregion

        #region Private Methods
        private bool RefreshRequired()
        {
            if (_profile == null)
                return false;

            long delta = DateTime.Now.Ticks - _last;
            if (delta > Interval)
                return false;

            return true;
        }
        #endregion

        #region Fields
        private readonly AsyncLock _mutex = new AsyncLock();
        private static long _last = DateTime.Now.Ticks;
        private static SourceRepositoryProfile _profile;
        #endregion

        #region Constants
        private const long Interval = 10000L * 1000L * 60L * 30L;
        #endregion
    }
}
