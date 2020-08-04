﻿using System.Threading.Tasks;
using Log.It;
using Log.It.With.NLog;
using Microsoft.Extensions.Configuration;
using NLog.Extensions.Logging;
using Test.It.With.XUnit;
using Xunit.Abstractions;

namespace Port.Server.IntegrationTests.TestFramework
{
    public class TestSpecificationAsync : XUnit2SpecificationAsync
    {
        static TestSpecificationAsync()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            NLog.LogManager.Configuration = new NLogLoggingConfiguration(config.GetSection("NLog"));
            LogFactory.Initialize(new NLogFactory(new LogicalThreadContext()));
        }

        public TestSpecificationAsync(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            NLogCapturingTarget.Subscribe += TestOutputHelper.WriteLine;
        }

        protected virtual Task TearDownAsync()
        {
            return Task.CompletedTask;
        }

        protected sealed override async Task DisposeAsync(bool disposing)
        {
            NLogCapturingTarget.Subscribe -= TestOutputHelper.WriteLine;
            await TearDownAsync()
                .ConfigureAwait(false);
            await base.DisposeAsync(disposing)
                .ConfigureAwait(false);
        }
    }
}