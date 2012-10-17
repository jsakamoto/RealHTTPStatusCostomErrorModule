using System;
using System.Linq;
using System.Web;
using System.Web.Configuration;

public class RealHTTPStatusCustomErrorModule : IHttpModule
{
    /// <summary>
    /// Initializes a module and prepares it to handle requests.
    /// </summary>
    /// <param name="context">An <see cref="T:System.Web.HttpApplication" /> that provides access to the methods, properties, and events common to all application objects within an ASP.NET application</param>
    public void Init(HttpApplication context)
    {
        context.PreSendRequestHeaders += PreSendRequestHeaders;
    }

    /// <summary>
    /// Occurs just before ASP.NET sends HTTP headers to the client.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
    void PreSendRequestHeaders(object sender, EventArgs e)
    {
        // If no exception occurs, nothing to do.
        var app = sender as HttpApplication;
        var err = app.Server.GetLastError();
        if (err == null) return;

        // If the configuration of custom errors is not enabled, nothing to do.
        var response = app.Response;
        if (app.Context.IsCustomErrorEnabled == false)
        {
            response.AddHeader("X-CustomErrorPage", "No");
            return;
        }

        // If the configuration of custom errors redirect mode is not rewrite, nothing to do.
        var customErrors = app.Context.GetSection("system.web/customErrors") as CustomErrorsSection;
        if (customErrors == null || customErrors.RedirectMode != CustomErrorsRedirectMode.ResponseRewrite) return;

        // Retrieve HTTP status code to respond.
        response.StatusCode = (err is HttpException) ? (err as HttpException).GetHttpCode() : 500;

        // Avoid IIS7 httpErrors handling.
        var trySkipIisCustomErrors = default(bool);
        if (bool.TryParse(WebConfigurationManager.AppSettings["RealHTTPStatusCustomErrorModule:TrySkipIisCustomErrors"], out trySkipIisCustomErrors) == false)
            trySkipIisCustomErrors = true;
        response.TrySkipIisCustomErrors = trySkipIisCustomErrors;

        // Make 'X-ErrPageUrl' and 'X-CustomErrorPage' custom response header.
        var errPageUrl = customErrors.Errors.AllKeys.Contains(response.StatusCode.ToString()) ?
            customErrors.Errors[response.StatusCode.ToString()].Redirect :
            customErrors.DefaultRedirect;
        if (errPageUrl.StartsWith("/") == false)
        {
            if (errPageUrl.StartsWith("~/")) errPageUrl = errPageUrl.Substring(2);
            var appVPath = HttpRuntime.AppDomainAppVirtualPath;
            if (appVPath.EndsWith("/") == false) appVPath += "/";
            errPageUrl = appVPath + errPageUrl;
        }
        response.AddHeader("X-ErrPageUrl", errPageUrl);
        response.AddHeader("X-CustomErrorPage", "Yes");
    }

    /// <summary>
    /// Disposes of the resources (other than memory) used by the module that implements <see cref="T:System.Web.IHttpModule" />.
    /// </summary>
    public void Dispose()
    {
        // NOP
    }
}
