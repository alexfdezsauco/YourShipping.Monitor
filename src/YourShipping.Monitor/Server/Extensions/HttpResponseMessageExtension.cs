namespace YourShipping.Monitor.Server.Extensions
{
    using System.Net.Http;

    public static class HttpResponseMessageExtension
    {
        public static bool IsCaptchaRedirectResponse(this HttpResponseMessage @this)
        {
            return @this.RequestMessage.RequestUri.AbsoluteUri.EndsWith("captcha.aspx") || @this.RequestMessage.RequestUri.AbsoluteUri.EndsWith("captch.aspx");
        }

        public static bool IsSignInRedirectResponse(this HttpResponseMessage @this)
        {
            return @this.RequestMessage.RequestUri.AbsoluteUri.Contains("/SignIn.aspx?ReturnUrl=");
        }

        public static bool IsStoreClosedRedirectResponse(this HttpResponseMessage @this)
        {
            return @this.RequestMessage.RequestUri.AbsoluteUri.EndsWith("StoreClosed.aspx");
        }
    }
}