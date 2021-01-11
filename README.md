[![Build Status](https://dev.azure.com/alexfdezsauco/External%20Repositories%20Builds/_apis/build/status/alexfdezsauco.YourShipping.Monitor?branchName=master)](https://dev.azure.com/alexfdezsauco/External%20Repositories%20Builds/_build/latest?definitionId=3&branchName=master)

# YourShipping.Monitor

YourShipping (a.k.a. TuEnvio) has no public monitoring and notifications options. This is a basic app to monitoring specific departments and products from its well-known uri.

The goal is "similar" to [camelcamelcamel](https://camelcamelcamel.com) but in this case the target is [TuEnvio](https://www.tuenvio.cu/).

You're right, who am I kidding? The goal is buy ;).

![Departments Monitor](media/departments-page.png "Departments Monitor")

![Departments Monitor](media/products-page.png "Products Monitor")

## Create your own telegram bot

Follow the [Bots: An introduction for developers](https://core.telegram.org/bots) tutorial to create your telegram bot and save your telegram bot token.  

## Build

> **ALERT**: Linux users need to [Install Mono](https://www.mono-project.com/docs/getting-started/install/linux/) required to run `GitVersion.CommandLine` 

### Build binaries for your current OS.

- [Install .NET Core on Windows, Linux, and macOS](https://docs.microsoft.com/en-us/dotnet/core/install/)
- Install Cake Build tools

      > dotnet new tool-manifest
      > dotnet tool install Cake.Tool

- Run `Publish` task

      > cd %CLONE_DIR%
      > dotnet cake -target="Publish" -configuration="Release"

### Build docker image
    
- Run DockerBuild task.
      
      > cd %CLONE_DIR%
      > dotnet cake -target="DockerBuild"
    
## Execute the container

    > mkdir %APP_DIR%/data
    > mkdir %APP_DIR%/logs
    > docker run -d --name your-shipping-monitor --rm -p 80:80 -v %APP_DIR%/data:/app/data -v %APP_DIR%/logs:/app/logs -e "TelegramBot:Token=%TELEGRAM_BOT_TOKEN%" your-shipping-monitor:latest
    
### Authenticating with using cookies.txt

You can export the `cookies.txt` by using theses extensions:

- *Chrome / Microsoft Edge*: [cookiestxt](https://chrome.google.com/webstore/detail/cookiestxt/njabckikapfpffapmjgojcnbfjonfjfg)
- *Microsoft Edge*: [get-cookiestxt](https://microsoftedge.microsoft.com/addons/detail/get-cookiestxt/helleheikohejgehaknifdkcfcmceeip)

and save it in this location `%APP_DIR%/data`.

### Unattended authentication bypassing the 1st captcha

> **ALERT**: Linux users should make sure to install the following libraries: `libleptonica-dev, libgif7, libjpeg62, libopenjp2-7, libpng16-16, libtiff5, libwebp6, libc6-dev, libgdiplus`

> **ALERT**: If this method doesn't work for you, just use the cookies.txt file.

- Add a configuration section to for credentials 

        "Credentials": {
            "Username": "%USERNAME%",
            "Password": "%PASSWORD%"
        }

- For docker run use this environment variables

	-e "Credentials:Username=%USERNAME%" -e "Credentials:Password=PASSWORD"

### Mount products in the shopping cart bypassing the 2nd captcha

> **ALERT**: The captcha database might require updates over time, so if you notice a new captcha challenge, just solve it and create a PR in the database [repository](https://github.com/alexfdezsauco/YourShipping.Monitor-ReCaptchasDB).

- For docker run

        > mkdir %APP_DIR%/data
        > mkdir %APP_DIR%/logs
        > mkdir %APP_DIR%/captchas
        > git clone --progress -v "https://github.com/alexfdezsauco/YourShipping.Monitor-ReCaptchasDB.git" "%APP_DIR%/re-captchas"
        > docker run -d --name your-shipping-monitor --rm -p 80:80 -v %APP_DIR%/data:/app/data -v %APP_DIR%/logs:/app/logs -v %APP_DIR%/captchas:/app/captchas -v %APP_DIR%/re-captchas:/app/re-captchas -e "TelegramBot:Token=%TELEGRAM_BOT_TOKEN%" -e "Credentials:Username=%USERNAME%" -e "Credentials:Password=PASSWORD" your-shipping-monitor:latest
