#addin nuget:?package=Cake.Docker&version=0.11.1
#addin nuget:?package=Cake.FileHelpers&version=3.3.0
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
      StartProcess("dotnet", new ProcessSettings
      {
          Arguments = new ProcessArgumentBuilder()
          .Append("gitversion")
          .Append("/output")
          .Append("buildserver")
          .Append("/nofetch")
          .Append("/updateassemblyinfo")
      });

      IEnumerable<string> redirectedStandardOutput;
      StartProcess("dotnet", new ProcessSettings
      {
          Arguments = new ProcessArgumentBuilder()
          .Append("gitversion")
          .Append("/output")
          .Append("json")
	  .Append("/nofetch"),
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
              Tag = new[] { $"{DockerRepositoryPrefix}{outputImage}:{NuGetVersionV2}", $"{DockerRepositoryPrefix}{outputImage}:latest" },
              Network = "bridge"
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