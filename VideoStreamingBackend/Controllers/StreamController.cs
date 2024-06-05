using System;
using System.Diagnostics;
using System.IO;
using System.Resources;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Xabe.FFmpeg;

namespace VideoStreamingBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StreamController : ControllerBase
    {
        private static readonly string VideoFilePath = @"C:\Users\konta\Desktop\test.mp4";
        private static readonly string outputDirectory = @"C:\Users\konta\source\repos\VideoStreamingBackend\VideoStreamingBackend\output_directory\test";


        [HttpGet("{streamName}/{ts}")]
        public async Task<IActionResult> GetVideoTs([FromRoute] string streamName, [FromRoute] string ts)
        {
            var playlistName = streamName.Split(".")[0];
            var playListFilePath = Path.Combine(outputDirectory, playlistName);

            if (!Directory.Exists(playListFilePath)) return BadRequest();

            var tsFilePath = Path.Combine(playListFilePath, ts);
            if (!System.IO.File.Exists(tsFilePath)) return BadRequest();

            var tsFile = await System.IO.File.ReadAllBytesAsync(tsFilePath);
            return File(tsFile, "video/mp2t", true);
        }

        [HttpGet("stream/{streamName}/{fileName}/{ts}")]
        public async Task<IActionResult> GetVideoStream([FromRoute] string streamName, [FromRoute] string fileName, [FromRoute] string ts)
        {
            var playlistName = streamName.Split(".")[0];
            var playListFilePath = Path.Combine(outputDirectory, playlistName);

            if (!Directory.Exists(playListFilePath)) return BadRequest();

            var streamFilePath = Path.Combine(playListFilePath, fileName, ts);

            var playlistFile = await System.IO.File.ReadAllBytesAsync(streamFilePath);
            return File(playlistFile, "application/vnd.apple.mpegurl", true);
        }

        [HttpGet("stream/{streamName}/{fileName}")]
        public async Task<IActionResult> GetVideoStream([FromRoute] string streamName, [FromRoute] string fileName)
        {
            var playlistName = streamName.Split(".")[0];
            var playListFilePath = Path.Combine(outputDirectory, playlistName);

            if (!Directory.Exists(playListFilePath)) return BadRequest();

            var streamFilePath = Path.Combine(playListFilePath, fileName);

            var playlistFile = await System.IO.File.ReadAllBytesAsync(streamFilePath);
            return File(playlistFile, "application/vnd.apple.mpegurl", true);

        }

        [HttpGet("stream/{streamName}")]
        public async Task<IActionResult> GetStreamMaster([FromRoute] string streamName)
        {
            var playlistName = streamName.Split(".")[0];
            var playListFilePath = Path.Combine(outputDirectory, playlistName);

            if (!Directory.Exists(playListFilePath)) return BadRequest();

            var streamFilePath = Path.Combine(playListFilePath, "master.m3u8");

            var playlistFile = await System.IO.File.ReadAllBytesAsync(streamFilePath);
            return File(playlistFile, "application/vnd.apple.mpegurl", true);

        }

        [HttpGet("create")]
        public async Task<IActionResult> CreateVideoM3U8(CancellationToken cancellationToken)
        {
            if (Directory.Exists(outputDirectory)) return BadRequest();

            Directory.CreateDirectory(outputDirectory);

            var ffmpegArgsList = new List<string>
            {
                $"-i \"{VideoFilePath}\"",
                "-filter_complex \"[0]split=3[v1][v2][v3]; [v1]copy[v1out]; [v2]scale=w=1280:h=720[v2out]; [v3]scale=w=640:h=360[v3out]\"",
                "-map \"[v1out]\"",
                "-c:v:0 libx264 -x264-params \"nal-hrd=cbr:force_cfr=1\" -b:v:0 5M -maxrate:v:0 5M -minrate:v:0 5M -bufsize:v:0 10M -preset slow -g 48 -sc_threshold 0 -keyint_min 48",
                "-map \"[v2out]\"",
                "-c:v:1 libx264 -x264-params \"nal-hrd=cbr:force_cfr=1\" -b:v:1 3M -maxrate:v:1 3M -minrate:v:1 3M -bufsize:v:1 3M -preset slow -g 48 -sc_threshold 0 -keyint_min 48",
                "-map \"[v3out]\"",
                "-c:v:2 libx264 -x264-params \"nal-hrd=cbr:force_cfr=1\" -b:v:2 1M -maxrate:v:2 1M -minrate:v:2 1M -bufsize:v:2 1M -preset slow -g 48 -sc_threshold 0 -keyint_min 48",
                "-map a:0 -c:a:0 aac -b:a:0 96k -ac 2",
                "-map a:0 -c:a:1 aac -b:a:1 96k -ac 2",
                "-map a:0 -c:a:2 aac -b:a:2 48k -ac 2",
                "-f hls -hls_time 2",
                "-hls_playlist_type vod -hls_flags independent_segments -hls_segment_type mpegts",
                $"-hls_segment_filename \"{outputDirectory}\\stream_%v\\data%02d.ts\"",
                $"-master_pl_name \"{outputDirectory}\\master.m3u8\"",
                "-var_stream_map \"v:0,a:0 v:1,a:1 v:2,a:2\"",
                $"{outputDirectory}\\stream_%v.m3u8"
            };

            var ffmpegArgs = string.Join(" ", ffmpegArgsList);

            var conversion = FFmpeg.Conversions.New();

            conversion.OnProgress += (sender, args) => Console.WriteLine($"[{args.Duration}/{args.TotalLength}] {args.Percent}%");
            conversion.OnDataReceived += (sender, args) => Console.WriteLine(args.Data);
            conversion.OnVideoDataReceived += (sender, args) => Console.WriteLine(args.Data);
            conversion.SetOverwriteOutput(true);

            var conversionResult = await conversion.Start(ffmpegArgs, cancellationToken);

            var m3U8Files = Directory.GetFiles(outputDirectory, "*.m3u8");

            foreach (var filePath in m3U8Files)
            {
                var fileContent = await System.IO.File.ReadAllTextAsync(filePath, cancellationToken);
                fileContent = fileContent.Replace("data", $"{Path.GetFileNameWithoutExtension(filePath)}/data");
                await System.IO.File.WriteAllTextAsync(filePath, fileContent, cancellationToken);
            }

            return Ok(conversionResult.Duration);
        }
    }
}
