string NuGetVersionV2 = "";
string SolutionFileName = "src/YourShipping.Monitor.sln";

string[] DockerFiles = new [] 
	{
	 "./deployment/docker/Dockerfile" 
	};
string[] OutputImages = new [] 
	{ 
		"alexfdezsauco/your-shipping-monitor" 
	} ;
string[] ComponentProjects  = System.Array.Empty<string>();