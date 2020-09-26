# YourShipping.Monitor

YourShipping (a.k.a. TuEnvio) has no public monitoring and notifications options. This is a basic app to monitoring specific departments and products from its well-known uri.

The goal is "similar" to [camelcamelcamel](https://camelcamelcamel.com) but in this case the target is [TuEnvio](https://www.tuenvio.cu/).

You're right, who am I kidding? The goal is buy ;).

![Departments Monitor](media/departments-page.png "Departments Monitor")

![Departments Monitor](media/products-page.png "Products Monitor")

## Create your own telegram bot

Follow the [Bots: An introduction for developers](https://core.telegram.org/bots) tutorial to create your telegram bot and save your telegram bot token.  

## Build

### Build binaries for your current OS.

- [Install .NET Core on Windows, Linux, and macOS](https://docs.microsoft.com/en-us/dotnet/core/install/)
- Install Cake Build tools

      > dotnet new tool-manifest
      > dotnet tool install Cake.Tool

- Run Publish task

      > cd %CLONE_DIR%
      > dotnet cake -target="Publish" -configuration="Release"

### Build docker image
    
- Run DockerBuild task.
      
      > cd %CLONE_DIR%
      > dotnet cake -target="DockerBuild" -configuration="Release"
    
## Execute the container

    > mkdir %APP_DIR%/data
    > mkdir %APP_DIR%/logs
    > docker run -d --name your-shipping-monitor --rm -p 80:80 -v %APP_DIR%/data:/app/data -v %APP_DIR%/logs:/app/logs -e "TelegramBot:Token=%TELEGRAM_BOT_TOKEN%" your-shipping-monitor:latest
    
### Mount products in the shopping cart using cookies.txt

In oder to mount products in the shopping cart the user must be authenticated. This is an "incomplete" feature. So, you can create a pull request ;) or just save the `cookies.txt` file in the data directory.

You can export the `cookies.txt` by using theses extensions:

- *Chrome / Microsoft Edge*: [cookiestxt](https://chrome.google.com/webstore/detail/cookiestxt/njabckikapfpffapmjgojcnbfjonfjfg)
- *Microsoft Edge*: [get-cookiestxt](https://microsoftedge.microsoft.com/addons/detail/get-cookiestxt/helleheikohejgehaknifdkcfcmceeip)


### Mount products in the shopping cart bypassing the captcha

> ALERT: Linux users must be sure that have installed the following libraries: `libleptonica-dev, libgif7 libjpeg62 libopenjp2-7 libpng16-16 libtiff5 libwebp6 libc6-dev libgdiplus`

- Add a configuration section to for credentials 

        "Credentials": {
            "Username": "%USERNAME%",
            "Password": "%PASSWORD%",
        }

- For docker run

        > mkdir %APP_DIR%/data
        > mkdir %APP_DIR%/logs
        > mkdir %APP_DIR%/captchas
        > docker run -d --name your-shipping-monitor --rm -p 80:80 -v %APP_DIR%/data:/app/data -v %APP_DIR%/logs:/app/logs -v %APP_DIR%/captcha:/app/captcha -e "TelegramBot:Token=%TELEGRAM_BOT_TOKEN%" -e -e "Credentials:Username=%USERNAME%" -e "Credentials:Password=PASSWORD" your-shipping-monitor:latest