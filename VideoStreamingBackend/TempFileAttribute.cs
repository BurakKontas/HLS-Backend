using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

namespace VideoStreamingBackend;

public class TempFileAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionArguments["file"] is not IFormFile file)
        {
            context.Result = new BadRequestObjectResult("Invalid file");
            return;
        }

        var fileExtension = Path.GetExtension(file.FileName).Split(".")[1];

        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.{fileExtension}");

        await using var fileStream = new FileStream(tempFilePath, FileMode.Create);
        await file.CopyToAsync(fileStream, context.HttpContext.RequestAborted);
        context.HttpContext.Items["TempFilePath"] = tempFilePath;

        try
        {
            await base.OnActionExecutionAsync(context, next);
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }

            context.Result = new StatusCodeResult(499);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }
}
