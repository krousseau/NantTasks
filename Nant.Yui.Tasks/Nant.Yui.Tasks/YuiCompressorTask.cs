using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using NAnt.Core;
using NAnt.Core.Attributes;
using NAnt.Core.Types;
using Yahoo.Yui.Compressor;

namespace Nant.Yui.Tasks
{
    [TaskName("yuicompressor")]
    public class YuiCompressorTask : Task
    {
        private DirectoryInfo _outputDir;

        public YuiCompressorTask()
        {
            InputFileSet = new FileSet();
        }

        #region Public Properties
        [TaskAttribute("basedir", Required = true)]
        public DirectoryInfo BaseDirectory { get; set; }
        
        /// <summary>
        /// Defaults to 'Debug'
        /// </summary>
        [TaskAttribute("debugdir")]
        public string DebugDirectoryName { get; set; }

        /// <summary>
        /// Defaults to 'Release'
        /// </summary>
        [TaskAttribute("releasedir")]
        public string ReleaseDirectoryName { get; set; }

        [BuildElement("fileset", Required = true)]
        public FileSet InputFileSet { get; set; }

        /// <summary>
        /// Should the task combine all of the input files into 1 output file?
        /// </summary>
        [TaskAttribute("combine")]
        [BooleanValidator]
        public bool Combine { get; set; }

        /// <summary>
        /// Defaults to master.js or master.css depending on the input file types
        /// </summary>
        [TaskAttribute("combinedfile")]
        public string CombinedFile { get; set; }
        #endregion

        //protected override void  InitializeXml(XmlNode elementNode, PropertyDictionary properties, FrameworkInfo framework)
        protected override void InitializeTask(XmlNode taskNode)
        {
            DefaultValues();

            if (BaseDirectory == null)
            {
                throw new BuildException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The 'basedir' attribute must be set to specify the output directory of the minified JS/CSS files."),
                    Location);
            }

            if (!BaseDirectory.Exists)
            {
                BaseDirectory.Create();
            }
            _outputDir = new DirectoryInfo(string.Format(@"{0}\{1}", BaseDirectory.FullName, ReleaseDirectoryName));
            if (!_outputDir.Exists)
            {
                _outputDir.Create();
            }

            if (InputFileSet == null)
            {
                throw new BuildException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The <fileset> element must be used to specify the JS/CSS files to Compress."),
                    Location);
            }
        }

        private void DefaultValues()
        {
            if (string.IsNullOrEmpty(ReleaseDirectoryName))
            {
                ReleaseDirectoryName = "Release";
            }
            if (string.IsNullOrEmpty(DebugDirectoryName))
            {
                DebugDirectoryName = "Debug";
            }
            if (string.IsNullOrEmpty(CombinedFile))
            {
                CombinedFile = "master";
            }
        }

        protected override void ExecuteTask()
        {
            if (InputFileSet.BaseDirectory == null)
            {
                InputFileSet.BaseDirectory = new DirectoryInfo(Project.BaseDirectory);
            }

            if(Combine)
            {
                Log(Level.Info, @"Compressing & Combining {0} JavaScript/CSS file(s) to '{1}\{2}'.",
                    InputFileSet.FileNames.Count, BaseDirectory.FullName, CombinedFile);
                CompressAndCombineFiles();
            }
            else
            {
                Log(Level.Info, @"Compressing {0} JavaScript/CSS file(s) to '{1}'.", InputFileSet.FileNames.Count, BaseDirectory.FullName);
                CompressIndividualFiles();
            }
        }

        /// <summary>
        /// Compresses an individual file and outputs it to the release directory with the same directory structure 
        /// as the debug file
        /// </summary>
        private void CompressIndividualFiles()
        {
            foreach (string srcPath in InputFileSet.FileNames)
            {
                var srcFile = GetSourcePath(srcPath);
                if (srcFile.Exists)
                {
                    var destPath = GetDestPath(srcFile);

                    Log(Level.Verbose, "Compressing '{0}' to '{1}'.", srcFile.FullName, destPath);

                    string fileText = File.ReadAllText(srcPath);
                    if (Path.GetExtension(srcFile.FullName).ToLower() == ".css")
                    {
                        File.WriteAllText(destPath.FullName, CssCompressor.Compress(fileText));
                    }
                    else if (Path.GetExtension(srcFile.FullName).ToLower() == ".js")
                    {
                        File.WriteAllText(destPath.FullName, JavaScriptCompressor.Compress(fileText, Verbose));
                    }
                    else
                    {
                        throw new BuildException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Expected a .css or .js extension. Got \"{0}\"",
                            Path.GetFileName(srcPath)),
                        Location);
                    }
                }
                else
                {
                    throw new BuildException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Could not find file '{0}' to Compress.",
                            srcFile.FullName),
                        Location);
                }
            }
        }

        /// <summary>
        /// Compresses all of the specified files and combines them into one output file, which was 
        /// specified in the Nant script
        /// </summary>
        private void CompressAndCombineFiles()
        {
            var sb = new StringBuilder();
            string combinedFilePath = string.Format(@"{0}\{1}\{2}", BaseDirectory.FullName, ReleaseDirectoryName, CombinedFile);

            foreach (string srcPath in InputFileSet.FileNames)
            {
                var srcFile = GetSourcePath(srcPath);

                if (srcFile.Exists)
                {
                    Log(Level.Verbose, "Compressing and combining '{0}' to '{1}'.", srcFile.FullName, combinedFilePath);

                    string fileText = File.ReadAllText(srcFile.FullName);
                    if (Path.GetExtension(srcFile.FullName).ToLower() == ".css")
                    {
                        if (!combinedFilePath.EndsWith(".css"))
                        {
                            combinedFilePath += ".css";
                        }
                        sb.Append(CssCompressor.Compress(fileText));
                    }
                    else if (Path.GetExtension(srcFile.FullName).ToLower() == ".js")
                    {
                        if (!combinedFilePath.EndsWith(".js"))
                        {
                            combinedFilePath += ".js";
                        }
                        sb.Append(JavaScriptCompressor.Compress(fileText, Verbose));
                    }
                    else
                    {
                        throw new BuildException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Expected a .css or .js extension. Got \"{0}\"",
                            Path.GetFileName(srcPath)),
                        Location);
                    }
                }
                else
                {
                    throw new BuildException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Could not find file '{0}' to Compress.",
                            srcFile.FullName),
                        Location);
                }
            }

            File.WriteAllText(combinedFilePath, sb.ToString());
        }

        private FileInfo GetSourcePath(string srcPath)
        {
            return new FileInfo(Path.Combine(InputFileSet.BaseDirectory.FullName, srcPath));
        }

        private FileInfo GetDestPath(FileInfo srcFile)
        {
            // Take the input file and simply replace the debug directory name with the release
            // directory name 
            string outFileName = srcFile.FullName.Replace(DebugDirectoryName, ReleaseDirectoryName);
            var outFileInfo = new FileInfo(outFileName);

            // If the directory doesn't exist that we are trying to output to 
            // then we need to create it
            if (outFileInfo.Directory != null && !outFileInfo.Directory.Exists)
            {
                outFileInfo.Directory.Create();
            }

            return outFileInfo;
        }
    }

}
