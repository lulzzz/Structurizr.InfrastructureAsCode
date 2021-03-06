using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Newtonsoft.Json.Linq;
using Structurizr.InfrastructureAsCode.Azure.Model;

namespace Structurizr.InfrastructureAsCode.Azure.ARM
{
    public abstract class AppServiceRenderer<TAppService> : AzureResourceRenderer<TAppService>
        where TAppService : AppService
    {
        protected override IEnumerable<IConfigurationValue> GetConfigurationValues(IHaveInfrastructure<TAppService> elementWithInfrastructure)
        {
            return elementWithInfrastructure.Infrastructure.Settings.Values.Concat(elementWithInfrastructure.Infrastructure.ConnectionStrings.Values);
        }

        protected void AddDependsOn(IHaveInfrastructure<AppService> elementWithInfrastructure, JObject template)
        {
            var dependsOn = DependsOn(elementWithInfrastructure);
            if (dependsOn.Any())
            {
                template["dependsOn"] = new JArray(dependsOn);
            }
        }

        protected virtual IEnumerable<string> DependsOn(IHaveInfrastructure<AppService> elementWithInfrastructure)
        {
            return Enumerable.Empty<string>();
        }

        protected virtual JObject Properties(IHaveInfrastructure<AppService> elementWithInfrastructure)
        {
            var appService = elementWithInfrastructure.Infrastructure;
            var properties = new JObject { ["name"] = appService.Name };
            return properties;
        }

        protected virtual void AddSubResources(IHaveInfrastructure<AppService> elementWithInfrastructure, JObject appService)
        {
            AppendSettingsResource(elementWithInfrastructure, elementWithInfrastructure.Infrastructure.Settings, appService);
            AppendSettingsResource(elementWithInfrastructure, elementWithInfrastructure.Infrastructure.ConnectionStrings, appService);
        }

        private void AppendSettingsResource(IHaveInfrastructure<AppService> elementWithInfrastructure, IEnumerable<AppServiceSetting> settings, JObject appService)
        {
            var resolvedSettings = settings
                .Where(s => s.Value.IsResolved)
                .ToArray();
            if (!resolvedSettings.Any())
            {
                return;
            }

            var isConnectionStrings = resolvedSettings.First() is AppServiceConnectionString;

            var resources = appService["resources"] as JArray;
            if (resources == null)
            {
                appService["resources"] = resources = new JArray();
            }

            var dependsOn = new JArray
            {
                elementWithInfrastructure.Infrastructure.Name
            };

            var properties = new JObject();

            var config = new JObject
            {
                ["apiVersion"] = ApiVersion,
                ["name"] = isConnectionStrings ? "connectionstrings" : "appsettings",
                ["type"] = "config",
                ["dependsOn"] = dependsOn,
                ["properties"] = properties
            };

            foreach (var setting in resolvedSettings)
            {
                var value = setting.Value;
                var dependentValue = value as IDependentConfigurationValue;
                if (dependentValue != null)
                {
                    dependsOn.Add(dependentValue.DependsOn.ResourceIdReference);
                }

                if (isConnectionStrings)
                {
                    properties[setting.Name] = new JObject
                    {
                        ["value"]  = JToken.FromObject(setting.Value.Value),
                        ["type"] = ((AppServiceConnectionString) setting).Type
                    };
                }
                else
                {
                    properties[setting.Name] = JToken.FromObject(setting.Value.Value);
                }
            }

            resources.Add(config);
        }

        protected const string ApiVersion = "2016-03-01";
    }
}