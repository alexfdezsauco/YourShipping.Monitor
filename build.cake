#addin "Cake.Docker"
#addin "Cake.FileHelpers"
#tool "nuget:?package=GitVersion.CommandLine&version=5.3.7"

#load "config.cake"

using System.Text.RegularExpressions;

var target = Argument("target", "Pack");
var buildConfiguration = Argument("Configuration", "Release");

using System.Net;
using System.Net.Sockets;

// var adapter = NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(i => i.Name == "Wi-Fi");
// var properties = adapter.GetIPProperties();



string localIpAddress;
using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
{
    socket.Connect("8.8.8.8", 65530);
    var endPoint = socket.LocalEndPoint as IPEndPoint;
    localIpAddress = endPoint.Address.ToString();
}

var dockerRepositoryProxy = EnvironmentVariable("DOCKER_REPOSITORY_PROXY") ?? $"mcr.microsoft.com";
var dockerRepository = EnvironmentVariable("DOCKER_REPOSITORY") ?? string.Empty;
var nugetRepositoryProxy = EnvironmentVariable("NUGET_REPOSITORY_PROXY") ?? $"https://api.nuget.org/v3/index.json";
var DockerRepositoryPrefix = string.IsNullOrWhiteSpace(dockerRepository) ? string.Empty : dockerRepository + "/";

Setup(context => {
    context.Tools.RegisterFile("./tools/GitVersion.CommandLine.5.3.7/tools/GitVersion.exe");
});

Task("UpdateVersion")
  .Does(() => 
  {
      FilePath gitVersionPath = Context.Tools.Resolve("GitVersion.exe");
      StartProcess(gitVersionPath, new ProcessSettings
      {
          Arguments = new ProcessArgumentBuilder()
          .Append("/output")
          .Append("buildserver")
          .Append("/nofetch")
          .Append("/updateassemblyinfo")
      });

      IEnumerable<string> redirectedStandardOutput;
      StartProcess(gitVersionPath, new ProcessSettings
      {
          Arguments = new ProcessArgumentBuilder()
          .Append("/output")
          .Append("json"),
          RedirectStandardOutput = true
      }, out redirectedStandardOutput);

      NuGetVersionV2 = redirectedStandardOutput.FirstOrDefault(s => s.Contains("NuGetVersionV2")).Split(':')[1].Trim(',').Trim('"');
});

Task("Restore")
  .Does(() => 
  {
      Information("Restoring Solution Packages");
      DotNetCoreRestore(SolutionFileName, new DotNetCoreRestoreSettings()
      {
          Sources = new[] { nugetRepositoryProxy },
          NoCache = true
      });

      Information("Configuration Service Plugins");
      var files = GetFiles("./deployment/packages/**/packages.config");
      Information(files);

      var packageDirectory = GetDirectories("./deployment/packages").FirstOrDefault();
      if (packageDirectory != null)
      {
          foreach (var file in files)
          {
              Information(file.GetDirectory().FullPath);
              var packageDirectoryTarget = "./output/packages/" + file.GetDirectory().FullPath.Substring(packageDirectory.FullPath.Length + 1);
              EnsureDirectoryExists(packageDirectoryTarget);
              CleanDirectory(packageDirectoryTarget);
              var packageFileName = packageDirectoryTarget + "/" + file.GetFilename();
              CopyFile(file, packageFileName);

              Dictionary<string, string> components = new Dictionary<string, string>();
              var matches = FindRegexMatchesGroupsInFile(packageFileName, @"<package\s+id=""([^""]+)""\s+version=""([^""]+)""\s+/>", RegexOptions.IgnoreCase | RegexOptions.Multiline);
              foreach (var match in matches)
              {
                  var componentName = match[1].Value;
                  var version = match[2].Value;
                  components.Add(componentName, version);
              }

              foreach (var component in components)
              {
                  Information("Looking for update for package {0}", component.Key);
                  var version = component.Value;
                  version = version.Trim('$', '{', '}');
                  var channels = version.Split(',').Select(s => s.Trim()).ToList();
                  List<Cake.Common.Tools.NuGet.List.NuGetListItem> packages = new List<Cake.Common.Tools.NuGet.List.NuGetListItem>();
                  foreach (var channel in channels)
                  {
                      Cake.Common.Tools.NuGet.List.NuGetListItem package = null;
                      var packageList = NuGetList(component.Key, new NuGetListSettings
                      {
                          AllVersions = false,
                          Prerelease = channel != "stable"
                      });

                      if (channel != "stable")
                      {
                          package = packageList.Where(p => p.Name == component.Key && (p.Version.Contains(channel) || System.Text.RegularExpressions.Regex.IsMatch(p.Version, @"\d+.\d+.\d+"))).OrderByDescending(p => p.Version).FirstOrDefault();
                      }
                      else
                      {
                          package = packageList.Where(p => p.Name == component.Key && System.Text.RegularExpressions.Regex.IsMatch(p.Version, @"\d+.\d+.\d+")).OrderByDescending(p => p.Version).FirstOrDefault();
                      }

                      if (package != null)
                      {
                          packages.Add(package);
                      }
                  }

                  {
                      var package = packages.OrderByDescending(p => p.Version).FirstOrDefault();
                      if (package != null)
                      {
                          Information("Found package {0}, current {1} latest {2}", package.Name, component.Value, package.Version);
                          XmlPoke(packageFileName, $"/packages/package[@id='{package.Name}']/@version", package.Version);
                      }
                  }
              }

              NuGetRestore(packageFileName, new NuGetRestoreSettings()
              {
                  PackagesDirectory = packageDirectoryTarget
              });
          }
    }
});

Task("Build")
  .IsDependentOn("UpdateVersion")
  .IsDependentOn("Restore")
  .Does(() => 
  {
      DotNetCoreBuild(
                  SolutionFileName,
                  new DotNetCoreBuildSettings()
                  {
                      Configuration = buildConfiguration,
                      ArgumentCustomization = args => args
                          .Append($"/p:Version={NuGetVersionV2}")
                          .Append($"/p:PackageVersion={NuGetVersionV2}")
                  });
  }); 

Task("Publish")
  .IsDependentOn("Build")
  .Does(() => 
  {
      DotNetCorePublish(
                  "src/YourShipping.Monitor/Server/YourShipping.Monitor.Server.csproj",
                  new DotNetCorePublishSettings
		  {
		     Configuration = buildConfiguration,
		     OutputDirectory = "./output/YourShipping.Monitor.Server",
                     ArgumentCustomization = args => args
                          .Append($"/p:Version={NuGetVersionV2}")
                          .Append($"/p:PackageVersion={NuGetVersionV2}")

		  });

        CopyFile("deployment/lib/x64/liblept1753.so", "output/YourShipping.Monitor.Server/x64/liblept1753.so");
        CopyFile("deployment/lib/x64/libtesseract3052.so", "output/YourShipping.Monitor.Server/x64/libtesseract3052.so");
  });   

Task("DockerBuild")
  .IsDependentOn("UpdateVersion")
  .Does(() => 
  {
      if (DockerFiles.Length != OutputImages.Length)
      {
          Error("DockerFiles.Length != OutputImages.Length");
      }

      var tarFileName = "dotnet.csproj.tar.gz";

      using (var process = StartAndReturnProcess("tar", new ProcessSettings { Arguments = $"-cf {tarFileName} -C src {System.IO.Path.GetFileName(SolutionFileName)}" }))
      {
          process.WaitForExit();
      }

      var srcFilePath = GetDirectories("src").FirstOrDefault();
      var files = GetFiles("./src/**/*.csproj");
      foreach (var file in files)
      {
          var relativeFilePath = srcFilePath.GetRelativePath(file);
          using (var process = StartAndReturnProcess("tar", new ProcessSettings { Arguments = $"-rf {tarFileName} -C src {relativeFilePath}" }))
          {
              process.WaitForExit();
          }
      }

      var deployFile = $"./deployment/marathon/deploy.ps1";
      if (FileExists(deployFile))
      {
          EnsureDirectoryExists($"./output/marathon");
          CopyFile(deployFile, "./output/marathon/deploy.ps1");
      }

      for (int i = 0; i < DockerFiles.Length; i++)
      {
          var outputImage = OutputImages[i];
          var dockerFile = DockerFiles[i];

          var taskFile = $"./deployment/marathon/{outputImage}/task.json";
          if (FileExists(taskFile))
          {
              EnsureDirectoryExists($"./output/marathon/{outputImage}");
              CleanDirectory($"./output/marathon/{outputImage}");

              var outputTaskFileName = $"./output/marathon/{outputImage}/task.json";
              CopyFile(taskFile, outputTaskFileName);

              ReplaceTextInFiles(outputTaskFileName, "${VERSION_NUMBER}", NuGetVersionV2);
          }

          var settings = new DockerImageBuildSettings()
          {
              File = dockerFile,
              BuildArg = new[] {$"DOCKER_REPOSITORY_PROXY={dockerRepositoryProxy}",
                                                  $"NUGET_REPOSITORY_PROXY={nugetRepositoryProxy}",
                                                  $"PACKAGE_VERSION={NuGetVersionV2}"},
              Tag = new[] { $"{DockerRepositoryPrefix}{outputImage}:{NuGetVersionV2}", $"{DockerRepositoryPrefix}{outputImage}:latest" }
          };
          DockerBuild(settings, "./");
      }
  });

Task("Pack")
  .IsDependentOn("Build")
  .Does(() => 
  {
    for (int i = 0; i < ComponentProjects.Length; i++)
    {
        var componentProject = ComponentProjects[i];
        var packageOutputDirectory = $"./output/nuget";
        var settings = new DotNetCorePackSettings
        {
            Configuration = buildConfiguration,
            OutputDirectory = packageOutputDirectory,
            ArgumentCustomization = args => args
                .Append($"/p:PackageVersion={NuGetVersionV2}")
                .Append($"/p:Version={NuGetVersionV2}")
        };

        DotNetCorePack(componentProject, settings);
    }
});

RunTarget(target);