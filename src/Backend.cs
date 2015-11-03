﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using SYMM_Backend;
using System.Threading;

namespace Flash_dl
{
    public static class Backend
    {
        #region Private fields

        private const string applicationName = "Flash-dl";
        private static readonly Version applicationVersion = Assembly.GetExecutingAssembly().GetName().Version;
        private static readonly string applicationVersionName = string.Format("v{0}.{1}", applicationVersion.Major, applicationVersion.Minor, applicationVersion.Build, applicationVersion.Revision);
        private static readonly string applicationVersionVerboseName = string.Format("v{0}.{1} Patch {2} Build {3}", applicationVersion.Major, applicationVersion.Minor, applicationVersion.Build, applicationVersion.Revision);

        // THIS WILL BE REPLACED BY OWN APi PROXY IN THE FUTURE
        // DO NOT use for production and REMOVE before pushing to public
        private static string APIKey = Properties.Settings.Default.youtubeApiKey;
        public static SYMMHandler symmBackend = new SYMMHandler(APIKey);

        // Videolist used to store all videos that are about to get downloaded
        private static List<YouTubeVideo> rawVideoList = new List<YouTubeVideo>();

        #endregion

        #region Private Methods

        public static void UpdateTitle()
        {
            Console.Title = string.Format("{0} ({1})", applicationName, applicationVersionName);
        }

        public static string Help(bool fromBadCommand = false, string command = "")
        {
            string output = "";

            if(fromBadCommand)
            {
                if(!string.IsNullOrEmpty(command))
                    output += string.Format("Command \"{0}\" not found.\nPrinting help.\n", command);
            }

            output += string.Format("\n{0} {1}", applicationName, applicationVersionVerboseName) + "\n\n";
            output += "DownloadVideo http://youtube.com/watch?v=abcdefg - Downloads the video from the URL to your Video folder.\n";
            output += "DownloadAudio http://youtube.com/watch?v=abcdefg - Downloads the video from the URL, coverts it to audio and saves it.\n";
            output += "DownloadPlaylist https://www.youtube.com/watch?list=abcdefg - Downloads a whole playlist from youtube.\n";
            output += "Exit - Closes the application. Goodbye!";
            return output;
        }

        public static void LoadByURL(SYMMSettings settings)
        {
            symmBackend.OnVideoInformationLoaded += (s, e) =>
            {
                rawVideoList.Add(e.Video);
            };

            Console.WriteLine("Loading data..");
            Backend.symmBackend.LoadVideosFromURL(settings.DownloadURL);
            Console.WriteLine("Loaded. Downloading, please wait..");
            Backend.StartDownload(settings);

            // Clean up after work is done
            rawVideoList = new List<YouTubeVideo>();
            symmBackend.ResetEvents();
        }

        private static void StartDownload(SYMMSettings settings)
        {
            // Check if folder exist, create it if not
            if (!Directory.Exists(settings.SavePath))
                Directory.CreateDirectory(settings.SavePath);

            // Reset event controlling max Downloads. If workingVideos >= maxSynDownloadVideo the thread waits for this event to set
            ManualResetEvent resetEvent = new ManualResetEvent(false);

            // Register changed download progress of one video Audio process counts as 75% work
            symmBackend.OnVideoDownloadProgressChanged += (dsender, deventargs) =>
            {
                // Show progress on GUI
                if (settings.ExtractAudio)
                {
                    DrawProgressBar((int)Math.Floor(deventargs.ProgressPercentage) / 4, 100, 60, '#');
                }
                else
                {
                    DrawProgressBar((int)Math.Floor(deventargs.ProgressPercentage), 100, 60, '#');
                }
            };

            // Register changed audio extraction progress of one video. Audio process counts as 25% work
            symmBackend.OnVideoAudioExtractionProgressChanged += (dsender, deventargs) =>
            {
                // Show progress on GUI
                if (settings.ExtractAudio)
                {
                    DrawProgressBar((int)Math.Floor(deventargs.ProgressPercentage) / 4 + 75, 100, 60, '#');
                }
                else
                {
                    DrawProgressBar((int)Math.Floor(deventargs.ProgressPercentage), 100, 60, '#');
                }
                
            };

            // Register finished download of one video
            symmBackend.OnVideoDownloadComplete += (dsender, deventargs) =>
            {
                Console.WriteLine(String.Format("\nVideo finsihed: \"{0}\"", deventargs.Video.VideoTitle));
            };

            // Register when a video failed to download
            symmBackend.OnVideoDownloadFailed += (dsender, deventargs) =>
            {
                Console.WriteLine(String.Format("Video failed to download: \"{0}\"", deventargs.Video.VideoTitle));
            };

            // We want to download every video in this list
            foreach (YouTubeVideo video in rawVideoList)
            {
                Console.WriteLine(String.Format("\"{0}\"", video.VideoTitle));

                // Prepare backend
                settings.PathSafefileName = symmBackend.BuildPathSafeName(video.VideoTitle);

                // Looks like we downloaded that already. Skip.
                if (Directory.GetFiles(settings.SavePath, settings.PathSafefileName + ".*").Length > 0)
                {
                    Console.WriteLine(String.Format("Looks like we already downloaded \"{0}\".\nPass 'noduplicatecheck' as parameter to ignore this.", video.VideoTitle));

                    // Skip the rest
                    continue;
                }

                // Tell backend to download the video spceifed to destination spceifed in the variable
                symmBackend.DownloadVideo(video, settings);
            }
        }

        /// <summary>
        /// Progress bar animation
        /// </summary>
        /// <param name="complete"></param>
        /// <param name="maxVal"></param>
        /// <param name="barSize"></param>
        /// <param name="progressCharacter"></param>
        private static void DrawProgressBar(int complete, int maxVal, int barSize, char progressCharacter)
        {
            Console.CursorVisible = false;
            int left = Console.CursorLeft;
            decimal perc = (decimal)complete / (decimal)maxVal;
            int chars = (int)Math.Floor(perc / ((decimal)1 / (decimal)barSize));
            string p1 = String.Empty, p2 = String.Empty;

            for (int i = 0; i < chars; i++)
                p1 += progressCharacter;
            for (int i = 0; i < barSize - chars; i++)
                p2 += progressCharacter;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(p1);
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write(p2);

            Console.ResetColor();
            Console.Write(" {0}%", (perc * 100).ToString("N2"));
            Console.CursorLeft = left;
        }

        #endregion
    }
}
