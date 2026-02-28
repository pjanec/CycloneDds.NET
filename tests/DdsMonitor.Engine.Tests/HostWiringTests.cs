using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class HostWiringTests
{
    [Fact]
    public void HostWiring_DiResolvesAllServices()
    {
        var settings = new Dictionary<string, string?>
        {
            ["DdsSettings:DomainId"] = "0",
            ["DdsSettings:PluginDirectories:0"] = "plugins",
            ["DdsSettings:UiRefreshHz"] = "30"
        };
        var configuration = new TestConfiguration(settings);
        var services = new ServiceCollection();

        services.AddDdsMonitorServices(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var scoped = scope.ServiceProvider;

        _ = provider.GetRequiredService<DdsSettings>();
        _ = provider.GetRequiredService<ITopicRegistry>();
        _ = provider.GetRequiredService<IDdsBridge>();
        _ = provider.GetRequiredService<ISampleStore>();
        _ = provider.GetRequiredService<IInstanceStore>();
        _ = provider.GetRequiredService<IEventBroker>();
        _ = provider.GetRequiredService<IFilterCompiler>();
        _ = provider.GetRequiredService<Channel<SampleData>>();
        _ = provider.GetRequiredService<ChannelReader<SampleData>>();
        _ = provider.GetRequiredService<ChannelWriter<SampleData>>();

        _ = scoped.GetRequiredService<IWindowManager>();
        _ = scoped.GetRequiredService<IWorkspaceState>();

        var hostedServices = provider.GetServices<IHostedService>();
        Assert.Contains(hostedServices, service => service is DdsIngestionService);
    }

    private sealed class TestConfiguration : IConfiguration
    {
        private readonly Dictionary<string, string?> _values;

        public TestConfiguration(Dictionary<string, string?> values)
        {
            _values = values;
        }

        public string? this[string key]
        {
            get => _values.TryGetValue(key, out var value) ? value : null;
            set => _values[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren()
        {
            return GetChildrenForPrefix(string.Empty);
        }

        public IChangeToken GetReloadToken()
        {
            return new CancellationChangeToken(default);
        }

        public IConfigurationSection GetSection(string key)
        {
            return new TestConfigurationSection(this, key);
        }

        private IEnumerable<IConfigurationSection> GetChildrenForPrefix(string prefix)
        {
            var normalizedPrefix = string.IsNullOrEmpty(prefix) ? string.Empty : prefix + ":";
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in _values.Keys)
            {
                if (!entry.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var remainder = entry.Substring(normalizedPrefix.Length);
                var segment = remainder.Split(':')[0];
                if (keys.Add(segment))
                {
                    var path = string.IsNullOrEmpty(prefix) ? segment : prefix + ":" + segment;
                    yield return new TestConfigurationSection(this, path);
                }
            }
        }

        private sealed class TestConfigurationSection : IConfigurationSection
        {
            private readonly TestConfiguration _root;

            public TestConfigurationSection(TestConfiguration root, string path)
            {
                _root = root;
                Path = path;
                Key = path.Split(':').Last();
            }

            public string Key { get; }

            public string Path { get; }

            public string? Value
            {
                get => _root[Path];
                set => _root[Path] = value;
            }

            public string? this[string key]
            {
                get => _root[$"{Path}:{key}"];
                set => _root[$"{Path}:{key}"] = value;
            }

            public IEnumerable<IConfigurationSection> GetChildren()
            {
                return _root.GetChildrenForPrefix(Path);
            }

            public IChangeToken GetReloadToken()
            {
                return _root.GetReloadToken();
            }

            public IConfigurationSection GetSection(string key)
            {
                return new TestConfigurationSection(_root, $"{Path}:{key}");
            }
        }
    }
}
