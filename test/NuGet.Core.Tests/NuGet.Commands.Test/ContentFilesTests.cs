﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class ContentFilesTests : IDisposable
    {
        [Fact]
        public async Task ContentFiles_VerifyLockFileChange_Changed()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";

            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);

            var file = new FileInfo(Path.Combine(repository, "packageA.1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("contentFiles/any/any/a/file1.txt", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/b/file1.txt", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                            <id>packageA</id>
                            <version>1.0.0</version>
                            <title />
                            <contentFiles>
                                <files include=""**/*.txt"" copyToOutput=""true"" />
                            </contentFiles>
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var configJson = JObject.Parse(@"{
                ""dependencies"": {
                ""packageA"": ""1.0.0""
                },
                ""frameworks"": {
                ""_FRAMEWORK_"": {}
                }
            }".Replace("_FRAMEWORK_", framework));

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var format = new LockFileFormat();
            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var fromDisk = format.Read(request.LockFilePath);

            fromDisk.Targets.Single()
                .Libraries
                .Single()
                .ContentFiles
                .First()
                .Properties["copyToOutput"] = "False";

            // Assert
            Assert.False(fromDisk.Equals(result.LockFile));
            Assert.NotEqual(fromDisk.GetHashCode(), result.LockFile.GetHashCode());
        }

        [Fact]
        public async Task ContentFiles_VerifyLockFileChange_NoChange()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";

            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);

            var file = new FileInfo(Path.Combine(repository, "packageA.1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("contentFiles/any/any/a/file1.txt", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                            <id>packageA</id>
                            <version>1.0.0</version>
                            <title />
                            <contentFiles>
                                <files include=""**/*.txt"" copyToOutput=""TRUE"" />
                            </contentFiles>
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var configJson = JObject.Parse(@"{
                ""dependencies"": {
                ""packageA"": ""1.0.0""
                },
                ""frameworks"": {
                ""_FRAMEWORK_"": {}
                }
            }".Replace("_FRAMEWORK_", framework));

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var format = new LockFileFormat();
            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var fromDisk = format.Read(request.LockFilePath);

            // Assert
            Assert.True(fromDisk.Equals(result.LockFile));
            Assert.Equal(fromDisk.GetHashCode(), result.LockFile.GetHashCode());
        }

        [Fact]
        public async Task ContentFiles_IgnoreBadFolders()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";

            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);

            var file = new FileInfo(Path.Combine(repository, "packageA.1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("contentFiles/i-n-valid/any/file.txt", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/file.txt", new byte[] { 0 });
                zip.AddEntry("contentFiles/file.txt", new byte[] { 0 });
                zip.AddEntry("contentFiles/_._", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                            <id>packageA</id>
                            <version>1.0.0</version>
                            <title />
                            <contentFiles>
                                <files include=""**/*"" buildAction=""Compile"" />
                            </contentFiles>
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var configJson = JObject.Parse(@"{
                ""dependencies"": {
                ""packageA"": ""1.0.0""
                },
                ""frameworks"": {
                ""_FRAMEWORK_"": {}
                }
            }".Replace("_FRAMEWORK_", framework));

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);
            var count = target.Libraries.Single().ContentFiles.Count;

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(0, count);
        }

        [Fact]
        public async Task ContentFiles_NormalizeBuildActionNames()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";

            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);

            var file = new FileInfo(Path.Combine(repository, "packageA.1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("contentFiles/any/any/a/file1.txt", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                            <id>packageA</id>
                            <version>1.0.0</version>
                            <title />
                            <contentFiles>
                                <files include=""**/*.txt"" buildAction=""compile"" />
                            </contentFiles>
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var configJson = JObject.Parse(@"{
                ""dependencies"": {
                ""packageA"": ""1.0.0""
                },
                ""frameworks"": {
                ""_FRAMEWORK_"": {}
                }
            }".Replace("_FRAMEWORK_", framework));

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);
            var contentFile = target.Libraries.Single().ContentFiles.Single();

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal("Compile", contentFile.Properties["buildAction"]);
        }

        [Fact]
        public async Task ContentFiles_InvalidBuildAction()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";

            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);

            var file = new FileInfo(Path.Combine(repository, "packageA.1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("contentFiles/any/any/a/file1.txt", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                            <id>packageA</id>
                            <version>1.0.0</version>
                            <title />
                            <contentFiles>
                                <files include=""**/*"" copyToOutput=""true"" buildAction=""BAD!"" />
                            </contentFiles>
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var configJson = JObject.Parse(@"{
                ""dependencies"": {
                ""packageA"": ""1.0.0""
                },
                ""frameworks"": {
                ""_FRAMEWORK_"": {}
                }
            }".Replace("_FRAMEWORK_", framework));

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var command = new RestoreCommand(logger, request);

            // Act
            Exception exception = null;

            try
            {
                var result = await command.ExecuteAsync();
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            // Assert
            var expected = "Package 'packageA.1.0.0' specifies an invalid build action 'BAD!' for file 'contentFiles/any/any/a/file1.txt'.";
            Assert.Equal(expected, exception.Message);
        }

        [Fact]
        public async Task ContentFiles_NuspecContentFilesGlobbingDirectoryAboveRoot()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";

            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);

            var file = new FileInfo(Path.Combine(repository, "packageA.1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("contentFiles/any/any/a/file1.txt", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/b/file1.txt", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/a/a/file1.txt", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/a/b/file2.txt", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                            <id>packageA</id>
                            <version>1.0.0</version>
                            <title />
                            <contentFiles>
                                <files include="".././././**/*.txt"" copyToOutput=""true"" />
                                <files include=""**/*.cs"" exclude=""../../../../**/*.cs"" copyToOutput=""true"" />
                            </contentFiles>
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var configJson = JObject.Parse(@"{
                ""dependencies"": {
                ""packageA"": ""1.0.0""
                },
                ""frameworks"": {
                ""_FRAMEWORK_"": {}
                }
            }".Replace("_FRAMEWORK_", framework));

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);
            var files = target.Libraries.Single().ContentFiles;

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(4, target.Libraries.Single().ContentFiles.Count);
            Assert.True(files.All(item => item.Properties["copyToOutput"] == "False"));
        }

        [Fact]
        public async Task ContentFiles_NuspecContentFilesGlobbingSupportExcludeDirSet()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";

            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);

            var file = new FileInfo(Path.Combine(repository, "packageA.1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("contentFiles/any/any/a/file1.txt", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/b/file1.txt", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/a/a/file1.txt", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/a/b/file2.txt", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                            <id>packageA</id>
                            <version>1.0.0</version>
                            <title />
                            <contentFiles>
                                <files include=""**/*"" exclude=""**/a/b/*.txt"" copyToOutput=""true"" />
                            </contentFiles>
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var configJson = JObject.Parse(@"{
                ""dependencies"": {
                ""packageA"": ""1.0.0""
                },
                ""frameworks"": {
                ""_FRAMEWORK_"": {}
                }
            }".Replace("_FRAMEWORK_", framework));

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var files1 = target.Libraries.Single().ContentFiles.Where(item => item.Path.Contains("file1.txt")).ToList();
            var files2 = target.Libraries.Single().ContentFiles.Where(item => item.Path.Contains("file2.txt")).ToList();

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(4, target.Libraries.Single().ContentFiles.Count);
            Assert.True(files1.All(item => item.Properties["copyToOutput"] == "True"));
            Assert.True(files2.All(item => item.Properties["copyToOutput"] == "False"));
        }

        [Fact]
        public async Task ContentFiles_NuspecContentFilesGlobbingSupportExcludeDir()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";

            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);

            var file = new FileInfo(Path.Combine(repository, "packageA.1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("contentFiles/any/any/a/file.txt", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/b/file.txt", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/a/a/file.txt", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/a/b/file.txt", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                            <id>packageA</id>
                            <version>1.0.0</version>
                            <title />
                            <contentFiles>
                                <files include=""**/*"" exclude=""**/b"" copyToOutput=""true"" />
                            </contentFiles>
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var configJson = JObject.Parse(@"{
                ""dependencies"": {
                ""packageA"": ""1.0.0""
                },
                ""frameworks"": {
                ""_FRAMEWORK_"": {}
                }
            }".Replace("_FRAMEWORK_", framework));

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var bFiles = target.Libraries.Single().ContentFiles.Where(item => item.Path.Contains("/b/")).ToList();
            var aFiles = target.Libraries.Single().ContentFiles.Where(item => !item.Path.Contains("/b/")).ToList();

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(4, target.Libraries.Single().ContentFiles.Count);
            Assert.True(bFiles.All(item => item.Properties["copyToOutput"] == "False"));
            Assert.True(aFiles.All(item => item.Properties["copyToOutput"] == "True"));
        }

        [Fact]
        public async Task ContentFiles_NuspecContentFilesRuleOrdering()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";

            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);

            var file = new FileInfo(Path.Combine(repository, "packageA.1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("contentFiles/any/any/a/file.txt", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/b/file.txt", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/a/a/file.txt", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/a/b/file.txt", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                            <id>packageA</id>
                            <version>1.0.0</version>
                            <title />
                            <contentFiles>
                                <files include=""**/*"" buildAction=""None"" copyToOutput=""true"" />
                                <files include=""**/*/a/file.txt"" copyToOutput=""false"" />
                            </contentFiles>
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var configJson = JObject.Parse(@"{
                ""dependencies"": {
                ""packageA"": ""1.0.0""
                },
                ""frameworks"": {
                ""_FRAMEWORK_"": {}
                }
            }".Replace("_FRAMEWORK_", framework));

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);

            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var bFiles = target.Libraries.Single().ContentFiles.Where(item => item.Path.Contains("/b/")).ToList();
            var aFiles = target.Libraries.Single().ContentFiles.Where(item => !item.Path.Contains("/b/")).ToList();

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(4, target.Libraries.Single().ContentFiles.Count);
            Assert.True(bFiles.All(item => item.Properties["copyToOutput"] == "True"));
            Assert.True(aFiles.All(item => item.Properties["copyToOutput"] == "False"));
            Assert.True(bFiles.All(item => item.Properties["buildAction"] == "None"));
            Assert.True(aFiles.All(item => item.Properties["buildAction"] == "None"));
        }

        [Fact]
        public async Task ContentFiles_NuspecContentFilesGlobbingSupportIncludeDir()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";

            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);

            var file = new FileInfo(Path.Combine(repository, "packageA.1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("contentFiles/any/any/a/file.txt", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/b/file.txt", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/a/a/file.txt", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/a/b/file.txt", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                            <id>packageA</id>
                            <version>1.0.0</version>
                            <title />
                            <contentFiles>
                                <files include=""**/b/"" copyToOutput=""true"" />
                            </contentFiles>
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var configJson = JObject.Parse(@"{
                ""dependencies"": {
                ""packageA"": ""1.0.0""
                },
                ""frameworks"": {
                ""_FRAMEWORK_"": {}
                }
            }".Replace("_FRAMEWORK_", framework));

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var bFiles = target.Libraries.Single().ContentFiles.Where(item => item.Path.Contains("/b/")).ToList();
            var aFiles = target.Libraries.Single().ContentFiles.Where(item => !item.Path.Contains("/b/")).ToList();

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(4, target.Libraries.Single().ContentFiles.Count);
            Assert.True(bFiles.All(item => item.Properties["copyToOutput"] == "True"));
            Assert.True(aFiles.All(item => item.Properties["copyToOutput"] == "False"));
        }

        [Fact]
        public async Task ContentFiles_NuspecContentFilesGlobbingIgnoreDirectoryUp()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";

            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);

            var file = new FileInfo(Path.Combine(repository, "packageA.1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("contentFiles/any/any/config/config.xml", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/images/image.jpg", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/images/image2.jpg", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/net45/code/code.cs", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/net45/config/config.xml", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/net45/images/image.jpg", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/win8/_._", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                            <id>packageA</id>
                            <version>1.0.0</version>
                            <title />
                            <contentFiles>
                                <files include=""../**/images.jpg"" buildAction=""None"" copyToOutput=""true"" flatten=""true"" />
                            </contentFiles>
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var configJson = JObject.Parse(@"{
                ""dependencies"": {
                ""packageA"": ""1.0.0""
                },
                ""frameworks"": {
                ""_FRAMEWORK_"": {}
                }
            }".Replace("_FRAMEWORK_", framework));

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var contentFiles = target.Libraries.Single().ContentFiles;

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(3, contentFiles.Count);
            Assert.True(contentFiles.All(item => item.Properties["copyToOutput"] == "False"));
            Assert.True(contentFiles.All(item => item.Properties["buildAction"] == "Compile"));
        }

        [Fact]
        public async Task ContentFiles_NuspecContentFilesGlobbingSupportExclude()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";

            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);

            var file = new FileInfo(Path.Combine(repository, "packageA.1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("contentFiles/any/any/config/config.xml", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/images/image.jpg", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/images/image2.jpg", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/net45/code/code.cs", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/net45/config/config.xml", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/net45/images/image.jpg", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/win8/_._", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                            <id>packageA</id>
                            <version>1.0.0</version>
                            <title />
                            <contentFiles>
                                <files include=""**/*"" exclude=""**/*.xml"" buildAction=""None"" copyToOutput=""true"" flatten=""true"" />
                            </contentFiles>
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var configJson = JObject.Parse(@"{
                ""dependencies"": {
                ""packageA"": ""1.0.0""
                },
                ""frameworks"": {
                ""_FRAMEWORK_"": {}
                }
            }".Replace("_FRAMEWORK_", framework));

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var contentFiles = target.Libraries.Single().ContentFiles;

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(3, contentFiles.Count);
            Assert.Equal(2, contentFiles.Where(item => item.Properties["buildAction"] == "None").Count());
        }

        [Fact]
        public async Task ContentFiles_NuspecContentFilesGlobbingSupportIncludeAll()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";

            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);

            var file = new FileInfo(Path.Combine(repository, "packageA.1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("contentFiles/any/any/config/config.xml", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/images/image.jpg", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/images/image2.jpg", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/net45/code/code.cs", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/net45/config/config.xml", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/net45/images/image.jpg", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/win8/_._", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                            <id>packageA</id>
                            <version>1.0.0</version>
                            <title />
                            <contentFiles>
                                <files include=""**/*"" buildAction=""None"" copyToOutput=""true"" flatten=""true"" />
                            </contentFiles>
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var configJson = JObject.Parse(@"{
                ""dependencies"": {
                ""packageA"": ""1.0.0""
                },
                ""frameworks"": {
                ""_FRAMEWORK_"": {}
                }
            }".Replace("_FRAMEWORK_", framework));

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var contentFiles = target.Libraries.Single().ContentFiles;

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(3, contentFiles.Count);
            Assert.True(contentFiles.All(item => item.Properties["copyToOutput"] == "True"));
            Assert.True(contentFiles.All(item => item.Properties["outputPath"].IndexOf("/") == -1));
            Assert.True(contentFiles.All(item => item.Properties["buildAction"] == "None"));
        }

        [Fact]
        public async Task ContentFiles_NuspecContentFilesGlobbingIncludeExactRootFile()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";

            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);

            var file = new FileInfo(Path.Combine(repository, "packageA.1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("contentFiles/any/any/config.xml", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                            <id>packageA</id>
                            <version>1.0.0</version>
                            <title />
                            <contentFiles>
                                <files include=""any/any/config.xml"" buildAction=""None"" copyToOutput=""true"" flatten=""true"" />
                            </contentFiles>
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var configJson = JObject.Parse(@"{
                ""dependencies"": {
                ""packageA"": ""1.0.0""
                },
                ""frameworks"": {
                ""_FRAMEWORK_"": {}
                }
            }".Replace("_FRAMEWORK_", framework));

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var contentFile = target.Libraries.Single().ContentFiles.Single();

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.True(contentFile.Properties["buildAction"] == "None");
        }

        [Fact]
        public async Task ContentFiles_NuspecContentFilesGlobbingIncludeEmptyString()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";

            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);

            var file = new FileInfo(Path.Combine(repository, "packageA.1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("contentFiles/any/any/config.xml", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                            <id>packageA</id>
                            <version>1.0.0</version>
                            <title />
                            <contentFiles>
                                <files include="""" buildAction=""None"" copyToOutput=""true"" flatten=""true"" />
                            </contentFiles>
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var configJson = JObject.Parse(@"{
                ""dependencies"": {
                ""packageA"": ""1.0.0""
                },
                ""frameworks"": {
                ""_FRAMEWORK_"": {}
                }
            }".Replace("_FRAMEWORK_", framework));

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var contentFile = target.Libraries.Single().ContentFiles.Single();

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal("Compile", contentFile.Properties["buildAction"]);
        }

        [Fact]
        public async Task ContentFiles_DefaultActionsWithNoNuspecSettings()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";

            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);

            // Create a shared content package with no nuspec
            CreateSharedContentPackageWithNoNuspecSettings(repository);

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var configJson = JObject.Parse(@"{
                ""dependencies"": {
                ""packageA"": ""1.0.0""
                },
                ""frameworks"": {
                ""_FRAMEWORK_"": {}
                }
            }".Replace("_FRAMEWORK_", framework));

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var utilItem = target.Libraries.Single().ContentFiles
                .Single(item => item.Path == "contentFiles/cs/net45/code/util.cs.pp");

            var configItem = target.Libraries.Single().ContentFiles
                .Single(item => item.Path == "contentFiles/cs/net45/config/config.xml");

            var imageItem = target.Libraries.Single().ContentFiles
                .Single(item => item.Path == "contentFiles/cs/net45/images/image.jpg");

            var utilFSItem = target.Libraries.Single().ContentFiles
                .Single(item => item.Path == "contentFiles/fs/net45/code/util.fs.pp");

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(4, utilItem.Properties.Count);
            Assert.Equal("Compile", utilItem.Properties["buildAction"]);
            Assert.Equal("False", utilItem.Properties["copyToOutput"]);
            Assert.Equal("code/util.cs", utilItem.Properties["ppOutputPath"]);
            Assert.Equal(3, configItem.Properties.Count);
            Assert.Equal("Compile", configItem.Properties["buildAction"]);
            Assert.Equal("False", configItem.Properties["copyToOutput"]);
            Assert.Equal(3, imageItem.Properties.Count);
            Assert.Equal("Compile", imageItem.Properties["buildAction"]);
            Assert.Equal("False", imageItem.Properties["copyToOutput"]);
            Assert.Equal(4, utilFSItem.Properties.Count);
            Assert.Equal("Compile", utilFSItem.Properties["buildAction"]);
            Assert.Equal("False", utilFSItem.Properties["copyToOutput"]);
            Assert.Equal("code/util.fs", utilFSItem.Properties["ppOutputPath"]);
        }

        [Fact]
        public async Task ContentFiles_EmptyFolder()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "win8";

            // Act
            var result = await StandardSetup(framework, logger);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var item = target.Libraries.Single().ContentFiles
                .Single();

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(3, item.Properties.Count);
            Assert.Equal("None", item.Properties["buildAction"]);
            Assert.Equal("False", item.Properties["copyToOutput"]);
            Assert.Equal("cs", item.Properties["codeLanguage"]);
        }

        [Fact]
        public async Task ContentFiles_CopyToOutputSettings()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";

            // Act
            var result = await StandardSetup(framework, logger);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var helperCsItem = target.Libraries.Single().ContentFiles
                .Single(item => item.Path == "contentFiles/cs/net45/config/config.xml");

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(4, helperCsItem.Properties.Count);
            Assert.Equal("None", helperCsItem.Properties["buildAction"]);
            Assert.Equal("True", helperCsItem.Properties["copyToOutput"]);
            Assert.Equal("config/config.xml", helperCsItem.Properties["outputPath"]);
        }

        [Fact]
        public async Task ContentFiles_VerifyPPInLockFile()
        {
            // Arrange
            var logger = new TestLogger();
            var framework = "net46";

            // Act
            var result = await StandardSetup(framework, logger);

            var target = result.LockFile.GetTarget(NuGetFramework.Parse(framework), null);

            var helperCsItem = target.Libraries.Single().ContentFiles
                .Single(item => item.Path == "contentFiles/cs/net45/code/util.cs.pp");

            // Assert
            Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
            Assert.Equal(0, logger.Errors);
            Assert.Equal(0, logger.Warnings);
            Assert.Equal(4, helperCsItem.Properties.Count);
            Assert.Equal("code/util.cs", helperCsItem.Properties["ppOutputPath"]);
            Assert.Equal("Compile", helperCsItem.Properties["buildAction"]);
            Assert.Equal("False", helperCsItem.Properties["copyToOutput"]);
        }

        private async Task<RestoreResult> SetupWithRuntimes(string framework, NuGet.Logging.ILogger logger)
        {
            // Arrange
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);

            // Create a shared content package
            CreateSharedContentPackage(repository);
            CreateRuntimesPackage(repository);

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            var configJson = JObject.Parse(@"{
                  ""supports"": {
                      ""net46.app"": {},
                      ""uwp.10.0.app"": { },
                      ""dnxcore50.app"": { }
                    },
                  ""dependencies"": {
                    ""packageA"": ""1.0.0"",
                    ""runtimes"": ""1.0.0""
                  },
                  ""frameworks"": {
                    ""_FRAMEWORK_"": {}
                  }
                }".Replace("_FRAMEWORK_", framework));

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            return result;
        }

        private async Task<RestoreResult> StandardSetup(
            string framework,
            NuGet.Logging.ILogger logger)
        {
            return await StandardSetup(framework, logger, null);
        }

        private async Task<RestoreResult> StandardSetup(
            string framework,
            NuGet.Logging.ILogger logger,
            JObject configJson)
        {
            // Arrange
            var workingDir = TestFileSystemUtility.CreateRandomTestFolder();
            _testFolders.Add(workingDir);

            var repository = Path.Combine(workingDir, "repository");
            Directory.CreateDirectory(repository);
            var projectDir = Path.Combine(workingDir, "project");
            Directory.CreateDirectory(projectDir);
            var packagesDir = Path.Combine(workingDir, "packages");
            Directory.CreateDirectory(packagesDir);

            // Create a shared content package
            CreateSharedContentPackage(repository);

            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(repository));

            if (configJson == null)
            {
                configJson = JObject.Parse(@"{
                  ""dependencies"": {
                    ""packageA"": ""1.0.0""
                  },
                  ""frameworks"": {
                    ""_FRAMEWORK_"": {}
                  }
                }".Replace("_FRAMEWORK_", framework));
            }

            var specPath = Path.Combine(projectDir, "TestProject", "project.json");
            var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

            var request = new RestoreRequest(spec, sources, packagesDir);
            
            request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

            var command = new RestoreCommand(logger, request);

            // Act
            var result = await command.ExecuteAsync();
            result.Commit(logger);

            return result;
        }

        private static FileInfo CreateRuntimesPackage(string repositoryDir)
        {
            var file = new FileInfo(Path.Combine(repositoryDir, "runtimes.1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("runtime.json", GetRuntimeJson(), Encoding.UTF8);

                zip.AddEntry("runtimes.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                        <id>runtimes</id>
                        <version>1.0.0</version>
                        <title />
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            return file;
        }

        private static FileInfo CreateSharedContentPackage(string repositoryDir)
        {
            var file = new FileInfo(Path.Combine(repositoryDir, "packageA.1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("contentFiles/any/any/config/config.xml", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/images/image.jpg", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/images/image2.jpg", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/net45/code/util.cs.pp", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/net45/code/code.cs", new byte[] { 0 });
                zip.AddEntry("contentFiles/fs/net45/code/util.fs.pp", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/net45/config/config.xml", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/net45/images/image.jpg", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/win8/_._", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                            <id>packageA</id>
                            <version>1.0.0</version>
                            <title />
                            <contentFiles>
                                <files include=""cs/net45/config/config.xml"" buildAction=""none"" />
                                <files include=""cs/net45/config/config.xml"" copyToOutput=""true"" flatten=""false"" />
                                <files include=""cs/net45/images/image.jpg"" buildAction=""embeddedresource"" />
                            </contentFiles>
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            return file;
        }

        private static FileInfo CreateSharedContentPackageWithNoNuspecSettings(string repositoryDir)
        {
            var file = new FileInfo(Path.Combine(repositoryDir, "packageA.1.0.0.nupkg"));

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("contentFiles/any/any/config/config.xml", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/images/image.jpg", new byte[] { 0 });
                zip.AddEntry("contentFiles/any/any/images/image2.jpg", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/net45/code/util.cs.pp", new byte[] { 0 });
                zip.AddEntry("contentFiles/fs/net45/code/util.fs.pp", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/net45/config/config.xml", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/net45/images/image.jpg", new byte[] { 0 });
                zip.AddEntry("contentFiles/win8/_._", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
                        <metadata>
                            <id>packageA</id>
                            <version>1.0.0</version>
                            <title />
                        </metadata>
                        </package>", Encoding.UTF8);
            }

            return file;
        }

        private static string GetRuntimeJson()
        {
            return @"{
                ""supports"": {
                    ""uwp.10.0.app"": {
                            ""uap10.0"": [
                                ""win10-x86"",
                                ""win10-x86-aot"",
                                ""win10-x64"",
                                ""win10-x64-aot"",
                                ""win10-arm"",
                                ""win10-arm-aot""
                        ]
                    },
                    ""net46.app"": {
                        ""net46"": [
                            ""win-x86"",
                            ""win-x64""
                        ]
                    },
                    ""dnxcore50.app"": {
                        ""dnxcore50"": [
                            ""win7-x86"",
                            ""win7-x64""
                        ]
                    }
                }
            }";
        }

        public void Dispose()
        {
            // Clean up
            foreach (var folder in _testFolders)
            {
                TestFileSystemUtility.DeleteRandomTestFolders(folder);
            }
        }

        private ConcurrentBag<string> _testFolders = new ConcurrentBag<string>();
    }
}