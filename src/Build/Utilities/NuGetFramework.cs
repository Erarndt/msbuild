// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.Build.Utilities.FrameworkConstants;

namespace Microsoft.Build.Utilities
{
    internal class NuGetFramework : IEquatable<NuGetFramework>
    {
        private static readonly char[] CommaSeparator = new char[] { ',' };

        private readonly string _frameworkIdentifier;
        private readonly Version _frameworkVersion;
        private readonly string _frameworkProfile;
        private string? _targetFrameworkMoniker;
        private string? _targetPlatformMoniker;
        private int? _hashCode;

        public NuGetFramework(NuGetFramework framework)
            : this(framework.Framework, framework.Version, framework.Profile, framework.Platform, framework.PlatformVersion)
        {
        }

        public NuGetFramework(string framework)
            : this(framework, FrameworkConstants.EmptyVersion)
        {
        }

        public NuGetFramework(string framework, Version version)
            : this(framework, version, string.Empty, FrameworkConstants.EmptyVersion)
        {
        }

        private const int Version5 = 5;

        /// <summary>
        /// Creates a new NuGetFramework instance, with an optional profile (only available for netframework)
        /// </summary>
        public NuGetFramework(string frameworkIdentifier, Version frameworkVersion, string? frameworkProfile)
            : this(frameworkIdentifier, frameworkVersion, profile: frameworkProfile ?? string.Empty, platform: string.Empty, platformVersion: FrameworkConstants.EmptyVersion)
        {
        }

        /// <summary>
        /// Creates a new NuGetFramework instance, with an optional platform and platformVersion (only available for net5.0+)
        /// </summary>
        public NuGetFramework(string frameworkIdentifier, Version frameworkVersion, string platform, Version platformVersion)
            : this(frameworkIdentifier, frameworkVersion, profile: string.Empty, platform: platform, platformVersion: platformVersion)
        {
        }

        internal NuGetFramework(string frameworkIdentifier, Version frameworkVersion, string profile, string platform, Version platformVersion)
        {
            if (frameworkIdentifier == null) throw new ArgumentNullException(nameof(frameworkIdentifier));
            if (frameworkVersion == null) throw new ArgumentNullException(nameof(frameworkVersion));
            if (platform == null) throw new ArgumentNullException(nameof(platform));
            if (platformVersion == null) throw new ArgumentNullException(nameof(platformVersion));

            _frameworkIdentifier = frameworkIdentifier;
            _frameworkVersion = NormalizeVersion(frameworkVersion);
            _frameworkProfile = profile;

            IsNet5Era = (_frameworkVersion.Major >= Version5 && StringComparer.OrdinalIgnoreCase.Equals(FrameworkConstants.FrameworkIdentifiers.NetCoreApp, _frameworkIdentifier));
            Platform = IsNet5Era ? platform : string.Empty;
            PlatformVersion = IsNet5Era ? NormalizeVersion(platformVersion) : FrameworkConstants.EmptyVersion;
        }

        /// <summary>
        /// An unknown or invalid framework
        /// </summary>
        public static readonly NuGetFramework UnsupportedFramework = new NuGetFramework(FrameworkConstants.SpecialIdentifiers.Unsupported);

        /// <summary>
        /// A framework with no specific target framework. This can be used for content only packages.
        /// </summary>
        public static readonly NuGetFramework AgnosticFramework = new NuGetFramework(FrameworkConstants.SpecialIdentifiers.Agnostic);

        /// <summary>
        /// A wildcard matching all frameworks
        /// </summary>
        public static readonly NuGetFramework AnyFramework = new NuGetFramework(FrameworkConstants.SpecialIdentifiers.Any);

        /// <summary>
        /// Creates a NuGetFramework from a folder name using the default mappings.
        /// </summary>
        public static NuGetFramework Parse(string folderName)
        {
            return Parse(folderName, DefaultFrameworkNameProvider.Instance);
        }

        /// <summary>
        /// Creates a NuGetFramework from a folder name using the given mappings.
        /// </summary>
        public static NuGetFramework Parse(string folderName, IFrameworkNameProvider mappings)
        {
            if (folderName == null) throw new ArgumentNullException(nameof(folderName));
            if (mappings == null) throw new ArgumentNullException(nameof(mappings));

            Debug.Assert(folderName.IndexOf(";", StringComparison.Ordinal) < 0, "invalid folder name, this appears to contain multiple frameworks");

            NuGetFramework framework = folderName.IndexOf(',') > -1
                ? ParseFrameworkName(folderName, mappings)
                : ParseFolder(folderName, mappings);

            return framework;
        }

        /// <summary>
        /// Creates a NuGetFramework from a .NET FrameworkName
        /// </summary>
        public static NuGetFramework ParseFrameworkName(string frameworkName, IFrameworkNameProvider mappings)
        {
            if (frameworkName == null) throw new ArgumentNullException(nameof(frameworkName));
            if (mappings == null) throw new ArgumentNullException(nameof(mappings));

            string[] parts = frameworkName.Split(CommaSeparator, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();

            // if the first part is a special framework, ignore the rest
            if (!TryParseSpecialFramework(parts[0], out NuGetFramework? result))
            {
                ParseFrameworkNameParts(mappings, parts, out string? framework, out Version version, out string? profile);

                if (version.Major >= 5
                    && StringComparer.OrdinalIgnoreCase.Equals(FrameworkConstants.FrameworkIdentifiers.NetCoreApp, framework))
                {
                    result = new NuGetFramework(framework, version, string.Empty, FrameworkConstants.EmptyVersion);
                }
                else
                {
                    result = new NuGetFramework(framework, version, profile);
                }
            }

            return result;
        }

        /// <summary>
        /// A set of special and common frameworks that can be returned from the list of constants without parsing
        /// Using the interned frameworks here optimizes comparisons since they can be checked by reference.
        /// This is designed to optimize
        /// </summary>
        private static bool TryParseCommonFramework(string frameworkString, [NotNullWhen(true)] out NuGetFramework? framework)
        {
            framework = null;

            frameworkString = frameworkString.ToLowerInvariant();

            switch (frameworkString)
            {
                case "dotnet":
                case "dotnet50":
                case "dotnet5.0":
                    framework = FrameworkConstants.CommonFrameworks.DotNet50;
                    break;
                case "net40":
                case "net4":
                    framework = FrameworkConstants.CommonFrameworks.Net4;
                    break;
                case "net403":
                    framework = FrameworkConstants.CommonFrameworks.Net403;
                    break;
                case "net45":
                    framework = FrameworkConstants.CommonFrameworks.Net45;
                    break;
                case "net451":
                    framework = FrameworkConstants.CommonFrameworks.Net451;
                    break;
                case "net452":
                    framework = FrameworkConstants.CommonFrameworks.Net452;
                    break;
                case "net46":
                    framework = FrameworkConstants.CommonFrameworks.Net46;
                    break;
                case "net461":
                    framework = FrameworkConstants.CommonFrameworks.Net461;
                    break;
                case "net462":
                    framework = FrameworkConstants.CommonFrameworks.Net462;
                    break;
                case "net463":
                    framework = FrameworkConstants.CommonFrameworks.Net463;
                    break;
                case "net47":
                    framework = FrameworkConstants.CommonFrameworks.Net47;
                    break;
                case "net471":
                    framework = FrameworkConstants.CommonFrameworks.Net471;
                    break;
                case "net472":
                    framework = FrameworkConstants.CommonFrameworks.Net472;
                    break;
                case "net48":
                    framework = FrameworkConstants.CommonFrameworks.Net48;
                    break;
                case "net481":
                    framework = FrameworkConstants.CommonFrameworks.Net481;
                    break;
                case "win8":
                    framework = FrameworkConstants.CommonFrameworks.Win8;
                    break;
                case "win81":
                    framework = FrameworkConstants.CommonFrameworks.Win81;
                    break;
                case "netstandard":
                    framework = FrameworkConstants.CommonFrameworks.NetStandard;
                    break;
                case "netstandard1.0":
                case "netstandard10":
                    framework = FrameworkConstants.CommonFrameworks.NetStandard10;
                    break;
                case "netstandard1.1":
                case "netstandard11":
                    framework = FrameworkConstants.CommonFrameworks.NetStandard11;
                    break;
                case "netstandard1.2":
                case "netstandard12":
                    framework = FrameworkConstants.CommonFrameworks.NetStandard12;
                    break;
                case "netstandard1.3":
                case "netstandard13":
                    framework = FrameworkConstants.CommonFrameworks.NetStandard13;
                    break;
                case "netstandard1.4":
                case "netstandard14":
                    framework = FrameworkConstants.CommonFrameworks.NetStandard14;
                    break;
                case "netstandard1.5":
                case "netstandard15":
                    framework = FrameworkConstants.CommonFrameworks.NetStandard15;
                    break;
                case "netstandard1.6":
                case "netstandard16":
                    framework = FrameworkConstants.CommonFrameworks.NetStandard16;
                    break;
                case "netstandard1.7":
                case "netstandard17":
                    framework = FrameworkConstants.CommonFrameworks.NetStandard17;
                    break;
                case "netstandard2.0":
                case "netstandard20":
                    framework = FrameworkConstants.CommonFrameworks.NetStandard20;
                    break;
                case "netstandard2.1":
                case "netstandard21":
                    framework = FrameworkConstants.CommonFrameworks.NetStandard21;
                    break;
                case "netcoreapp1.0":
                    framework = FrameworkConstants.CommonFrameworks.NetCoreApp10;
                    break;
                case "netcoreapp1.1":
                    framework = FrameworkConstants.CommonFrameworks.NetCoreApp11;
                    break;
                case "netcoreapp2.0":
                    framework = FrameworkConstants.CommonFrameworks.NetCoreApp20;
                    break;
                case "netcoreapp2.1":
                case "netcoreapp21":
                    framework = FrameworkConstants.CommonFrameworks.NetCoreApp21;
                    break;
                case "netcoreapp2.2":
                    framework = FrameworkConstants.CommonFrameworks.NetCoreApp22;
                    break;
                case "netcoreapp3.0":
                case "netcoreapp30":
                    framework = FrameworkConstants.CommonFrameworks.NetCoreApp30;
                    break;
                case "netcoreapp3.1":
                case "netcoreapp31":
                    framework = FrameworkConstants.CommonFrameworks.NetCoreApp31;
                    break;
                case "netcoreapp5.0":
                case "netcoreapp50":
                case "net5.0":
                case "net50":
                    framework = FrameworkConstants.CommonFrameworks.Net50;
                    break;
                case "netcoreapp6.0":
                case "netcoreapp60":
                case "net6.0":
                case "net60":
                    framework = FrameworkConstants.CommonFrameworks.Net60;
                    break;
                case "netcoreapp7.0":
                case "netcoreapp70":
                case "net7.0":
                case "net70":
                    framework = FrameworkConstants.CommonFrameworks.Net70;
                    break;
                case "netcoreapp8.0":
                case "netcoreapp80":
                case "net8.0":
                case "net80":
                    framework = FrameworkConstants.CommonFrameworks.Net80;
                    break;
                case "net9.0":
                    framework = FrameworkConstants.CommonFrameworks.Net90;
                    break;
                case "net10.0":
                    framework = FrameworkConstants.CommonFrameworks.Net10_0;
                    break;
            }

            return framework != null;
        }

        private static bool TryParseSpecialFramework(string frameworkString, [NotNullWhen(true)] out NuGetFramework? framework)
        {
            framework = null;

            if (StringComparer.OrdinalIgnoreCase.Equals(frameworkString, FrameworkConstants.SpecialIdentifiers.Any))
            {
                framework = AnyFramework;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(frameworkString, FrameworkConstants.SpecialIdentifiers.Agnostic))
            {
                framework = AgnosticFramework;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(frameworkString, FrameworkConstants.SpecialIdentifiers.Unsupported))
            {
                framework = UnsupportedFramework;
            }

            return framework != null;
        }

        private static string? SingleOrDefaultSafe(IEnumerable<string> items)
        {
            bool found = false;
            string result = null;
            foreach (var item in items)
            {
                if (found)
                {
                    return null;
                }

                found = true;
                result = item;
            }

            return result;
        }

        private static void ParseFrameworkNameParts(IFrameworkNameProvider mappings, string[] parts, out string framework, out Version version, out string? profile)
        {
            framework = mappings.TryGetIdentifier(parts[0], out string? mappedFramework)
                ? mappedFramework
                : parts[0];

            version = new Version(0, 0);
            profile = null;
            var versionPart = SingleOrDefaultSafe(parts.Where(s => s.IndexOf("Version=", StringComparison.OrdinalIgnoreCase) == 0));
            var profilePart = SingleOrDefaultSafe(parts.Where(s => s.IndexOf("Profile=", StringComparison.OrdinalIgnoreCase) == 0));
            if (!string.IsNullOrEmpty(versionPart))
            {
                var versionString = versionPart!.Split('=')[1].TrimStart('v');

                if (versionString.IndexOf('.') < 0)
                {
                    versionString += ".0";
                }
                
                version = Version.TryParse(versionString, out Version? parsedVersion)
                    ? parsedVersion
                    : throw new ArgumentException(nameof(versionString));
            }

            if (!string.IsNullOrEmpty(profilePart))
            {
                profile = profilePart!.Split('=')[1];
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(FrameworkConstants.FrameworkIdentifiers.Portable, framework)
                && !string.IsNullOrEmpty(profile)
                && profile!.Contains("-"))
            {
                // Frameworks within the portable profile are not allowed
                // to have profiles themselves #1869
                throw new ArgumentException(nameof(profile));
            }
        }

        /// <summary>
        /// Creates a NuGetFramework from a folder name using the default mappings.
        /// </summary>
        public static NuGetFramework ParseFolder(string folderName)
        {
            return ParseFolder(folderName, DefaultFrameworkNameProvider.Instance);
        }

        /// <summary>
        /// Creates a NuGetFramework from a folder name using the given mappings.
        /// </summary>
        public static NuGetFramework ParseFolder(string folderName, IFrameworkNameProvider mappings)
        {
            if (folderName == null)
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (mappings == null)
            {
                throw new ArgumentNullException(nameof(mappings));
            }

            if (folderName.IndexOf('%') > -1)
            {
                folderName = Uri.UnescapeDataString(folderName);
            }

            NuGetFramework? result;
            // first check if we have a special or common framework
            if (!TryParseSpecialFramework(folderName, out result)
                && !TryParseCommonFramework(folderName, out result))
            {
                // assume this is unsupported unless we find a match
                result = UnsupportedFramework;

                var parts = RawParse(folderName);

                if (parts != null)
                {
                    if (mappings.TryGetIdentifier(parts.Item1, out string? framework))
                    {
                        var version = FrameworkConstants.EmptyVersion;

                        if (parts.Item2 == null
                            || mappings.TryGetVersion(parts.Item2, out version))
                        {
                            string profileShort = parts.Item3;

                            if (version.Major >= 5
                                && (StringComparer.OrdinalIgnoreCase.Equals(FrameworkConstants.FrameworkIdentifiers.Net, framework)
                                    || StringComparer.OrdinalIgnoreCase.Equals(FrameworkConstants.FrameworkIdentifiers.NetCoreApp, framework)
                                   )
                                )
                            {
                                // net should be treated as netcoreapp in 5.0 and later
                                framework = FrameworkConstants.FrameworkIdentifiers.NetCoreApp;
                                if (!string.IsNullOrEmpty(profileShort))
                                {
                                    // Find a platform version if it exists and yank it out
                                    var platformChars = profileShort;
                                    var versionStart = 0;
                                    while (versionStart < platformChars.Length
                                           && IsLetterOrDot(platformChars[versionStart]))
                                    {
                                        versionStart++;
                                    }
                                    string platform = versionStart > 0 ? profileShort.Substring(0, versionStart) : profileShort;
                                    string? platformVersionString = versionStart > 0 ? profileShort.Substring(versionStart, profileShort.Length - versionStart) : null;

                                    // Parse the version if it's there.
                                    Version? platformVersion = FrameworkConstants.EmptyVersion;
                                    if ((string.IsNullOrEmpty(platformVersionString) || mappings.TryGetPlatformVersion(platformVersionString!, out platformVersion)))
                                    {
                                        result = new NuGetFramework(framework, version, platform ?? string.Empty, platformVersion ?? FrameworkConstants.EmptyVersion);
                                    }
                                    else
                                    {
                                        return result; // with result == UnsupportedFramework
                                    }
                                }
                                else
                                {
                                    result = new NuGetFramework(framework, version, string.Empty, FrameworkConstants.EmptyVersion);
                                }
                            }
                            else
                            {
                                if (!mappings.TryGetProfile(framework, profileShort, out string? profile))
                                {
                                    profile = profileShort ?? string.Empty;
                                }

                                if (StringComparer.OrdinalIgnoreCase.Equals(FrameworkConstants.FrameworkIdentifiers.Portable, framework))
                                {
                                    if (!mappings.TryGetPortableFrameworks(profileShort!, out IEnumerable<NuGetFramework>? clientFrameworks))
                                    {
                                        result = UnsupportedFramework;
                                    }
                                    else
                                    {
                                        var profileNumber = -1;
                                        if (mappings.TryGetPortableProfile(clientFrameworks, out profileNumber))
                                        {
                                            var portableProfileNumber = GetPortableProfileNumberString(profileNumber);
                                            result = new NuGetFramework(framework, version, portableProfileNumber);
                                        }
                                        else
                                        {
                                            result = new NuGetFramework(framework, version, profileShort);
                                        }
                                    }
                                }
                                else
                                {
                                    result = new NuGetFramework(framework, version, profile);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // If the framework was not recognized check if it is a deprecated framework
                    if (TryParseDeprecatedFramework(folderName, out NuGetFramework? deprecated))
                    {
                        result = deprecated;
                    }
                }
            }

            return result;
        }

        private static string GetPortableProfileNumberString(int profileNumber)
        {
            return String.Format(CultureInfo.InvariantCulture, "Profile{0}", profileNumber);
        }

        /// <summary>
        /// Attempt to parse a common but deprecated framework using an exact string match
        /// Support for these should be dropped as soon as possible.
        /// </summary>
        private static bool TryParseDeprecatedFramework(string s, [NotNullWhen(true)] out NuGetFramework? framework)
        {
            framework = null;

            switch (s)
            {
                case "45":
                case "4.5":
                    framework = FrameworkConstants.CommonFrameworks.Net45;
                    break;
                case "40":
                case "4.0":
                case "4":
                    framework = FrameworkConstants.CommonFrameworks.Net4;
                    break;
                case "35":
                case "3.5":
                    framework = FrameworkConstants.CommonFrameworks.Net35;
                    break;
                case "20":
                case "2":
                case "2.0":
                    framework = FrameworkConstants.CommonFrameworks.Net2;
                    break;
            }

            return framework != null;
        }

        private static Tuple<string, string?, string>? RawParse(string s)
        {
            string identifier;
            var profile = string.Empty;
            string? version = null;

            var chars = s.ToCharArray();

            var versionStart = 0;

            while (versionStart < chars.Length
                   && IsLetterOrDot(chars[versionStart]))
            {
                versionStart++;
            }

            if (versionStart > 0)
            {
                identifier = s.Substring(0, versionStart);
            }
            else
            {
                // invalid, we no longer support names like: 40
                return null;
            }

            var profileStart = versionStart;

            while (profileStart < chars.Length
                   && IsDigitOrDot(chars[profileStart]))
            {
                profileStart++;
            }

            var versionLength = profileStart - versionStart;

            if (versionLength > 0)
            {
                version = s.Substring(versionStart, versionLength);
            }

            if (profileStart < chars.Length)
            {
                if (chars[profileStart] == '-')
                {
                    var actualProfileStart = profileStart + 1;

                    if (actualProfileStart == chars.Length)
                    {
                        // empty profiles are not allowed
                        return null;
                    }

                    profile = s.Substring(actualProfileStart, s.Length - actualProfileStart);

                    foreach (var c in profile.ToArray())
                    {
                        // validate the profile string to AZaz09-+.
                        if (!IsValidProfileChar(c))
                        {
                            return null;
                        }
                    }
                }
                else
                {
                    // invalid profile
                    return null;
                }
            }

            return new Tuple<string, string?, string>(identifier, version, profile);
        }

        private static bool IsLetterOrDot(char c)
        {
            var x = (int)c;

            // "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"
            return (x >= 65 && x <= 90) || (x >= 97 && x <= 122) || x == 46;
        }

        private static bool IsDigitOrDot(char c)
        {
            var x = (int)c;

            // "0123456789"
            return (x >= 48 && x <= 57) || x == 46;
        }

        private static bool IsValidProfileChar(char c)
        {
            var x = (int)c;

            // letter, digit, dot, dash, or plus
            return (x >= 48 && x <= 57) || (x >= 65 && x <= 90) || (x >= 97 && x <= 122) || x == 46 || x == 43 || x == 45;
        }

        /// <summary>
        /// Creates a NuGetFramework from individual components
        /// </summary>
        public static NuGetFramework ParseComponents(string targetFrameworkMoniker, string? targetPlatformMoniker)
        {
            return ParseComponents(targetFrameworkMoniker, targetPlatformMoniker, DefaultFrameworkNameProvider.Instance);
        }

        /// <summary>
        /// Creates a NuGetFramework from individual components, using the given mappings.
        /// This method may have individual component preference, as described in the remarks.
        /// </summary>
        /// <remarks>
        /// Profiles and TargetPlatforms can't mix. As such the precedence order is profile over target platforms (TPI, TPV).
        /// .NETCoreApp,Version=v5.0 and later do not support profiles.
        /// Target Platforms are ignored for any frameworks not supporting them.
        /// This allows to handle the old project scenarios where the TargetPlatformIdentifier and TargetPlatformVersion may be set to Windows and v7.0 respectively.
        /// </remarks>
        internal static NuGetFramework ParseComponents(string targetFrameworkMoniker, string? targetPlatformMoniker, IFrameworkNameProvider mappings)
        {
            if (string.IsNullOrEmpty(targetFrameworkMoniker)) throw new ArgumentException(nameof(targetFrameworkMoniker));
            if (mappings == null) throw new ArgumentNullException(nameof(mappings));

            NuGetFramework? result;
            string targetFrameworkIdentifier;
            Version targetFrameworkVersion;
            var parts = GetParts(targetFrameworkMoniker);

            // if the first part is a special framework, ignore the rest
            if (TryParseSpecialFramework(parts[0], out result))
            {
                return result;
            }

            string? profile;
            string? targetFrameworkProfile;
            ParseFrameworkNameParts(mappings, parts, out targetFrameworkIdentifier, out targetFrameworkVersion, out targetFrameworkProfile);
            if (!mappings.TryGetProfile(targetFrameworkIdentifier, targetFrameworkProfile ?? string.Empty, out profile))
            {
                profile = targetFrameworkProfile;
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(FrameworkConstants.FrameworkIdentifiers.Portable, targetFrameworkIdentifier))
            {
                if (profile != null && mappings.TryGetPortableFrameworks(profile, out IEnumerable<NuGetFramework>? clientFrameworks))
                {
                    if (mappings.TryGetPortableProfile(clientFrameworks, out int profileNumber))
                    {
                        profile = GetPortableProfileNumberString(profileNumber);
                    }
                }
                else
                {
                    return UnsupportedFramework;
                }
            }

            var isNet5EraTfm = targetFrameworkVersion.Major >= 5 &&
                StringComparer.OrdinalIgnoreCase.Equals(FrameworkConstants.FrameworkIdentifiers.NetCoreApp, targetFrameworkIdentifier);

            if (!string.IsNullOrEmpty(profile) && isNet5EraTfm)
            {
                throw new ArgumentException(nameof(profile));
            }

            if (!string.IsNullOrEmpty(targetPlatformMoniker) && isNet5EraTfm)
            {
                string targetPlatformIdentifier;
                Version platformVersion;
                ParsePlatformParts(GetParts(targetPlatformMoniker!), out targetPlatformIdentifier, out platformVersion);
                result = new NuGetFramework(targetFrameworkIdentifier, targetFrameworkVersion, targetPlatformIdentifier ?? string.Empty, platformVersion);
            }
            else
            {
                result = new NuGetFramework(targetFrameworkIdentifier, targetFrameworkVersion, profile);
            }

            return result;
        }

        private static string[] GetParts(string targetPlatformMoniker)
        {
            return targetPlatformMoniker.Split(CommaSeparator, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
        }

        private static void ParsePlatformParts(string[] parts, out string targetPlatformIdentifier, out Version platformVersion)
        {
            targetPlatformIdentifier = parts[0];
            platformVersion = new Version(0, 0);
            var versionPart = SingleOrDefaultSafe(parts.Where(s => s.IndexOf("Version=", StringComparison.OrdinalIgnoreCase) == 0));
            if (!string.IsNullOrEmpty(versionPart))
            {
                var versionString = versionPart!.Split('=')[1].TrimStart('v');

                if (versionString.IndexOf('.') < 0)
                {
                    versionString += ".0";
                }

                platformVersion = Version.TryParse(versionString, out Version? parsedVersion)
                    ? parsedVersion
                    : throw new ArgumentException(nameof(versionString));
            }
        }

        /// <summary>
        /// Target framework
        /// </summary>
        public string Framework => _frameworkIdentifier;

        /// <summary>
        /// Target framework version
        /// </summary>
        public Version Version => _frameworkVersion;

        /// <summary>
        /// Framework Platform (net5.0+)
        /// </summary>
        public string Platform { get; }

        /// <summary>
        /// Framework Platform Version (net5.0+)
        /// </summary>
        public Version PlatformVersion { get; }

        /// <summary>
        /// True if the platform is non-empty
        /// </summary>
        public bool HasPlatform
        {
            get { return !string.IsNullOrEmpty(Platform); }
        }

        /// <summary>
        /// True if the profile is non-empty
        /// </summary>
        public bool HasProfile
        {
            get { return !string.IsNullOrEmpty(Profile); }
        }

        /// <summary>
        /// Target framework profile
        /// </summary>
        public string Profile => _frameworkProfile;

        /// <summary>The TargetFrameworkMoniker identifier of the current NuGetFramework.</summary>
        /// <remarks>Formatted to a System.Versioning.FrameworkName</remarks>
        public string DotNetFrameworkName
        {
            get
            {
                if (_targetFrameworkMoniker == null)
                {
                    _targetFrameworkMoniker = GetDotNetFrameworkName(DefaultFrameworkNameProvider.Instance);
                }
                return _targetFrameworkMoniker;
            }
        }

        /// <summary>The TargetFrameworkMoniker identifier of the current NuGetFramework.</summary>
        /// <remarks>Formatted to a System.Versioning.FrameworkName</remarks>
        public string GetDotNetFrameworkName(IFrameworkNameProvider mappings)
        {
            if (mappings == null)
            {
                throw new ArgumentNullException(nameof(mappings));
            }

            // Check for rewrites
            var framework = mappings.GetFullNameReplacement(this);

            if (framework.IsSpecificFramework)
            {
                var parts = new List<string>(3) { Framework };

                parts.Add(string.Format(CultureInfo.InvariantCulture, "Version=v{0}", GetDisplayVersion(framework.Version)));

                if (!string.IsNullOrEmpty(framework.Profile))
                {
                    parts.Add(string.Format(CultureInfo.InvariantCulture, "Profile={0}", framework.Profile));
                }

                return string.Join(",", parts);
            }
            else
            {
                return string.Format(CultureInfo.InvariantCulture, "{0},Version=v0.0", framework.Framework);
            }
        }

        /// <summary>The TargetPlatformMoniker identifier of the current NuGetFramework.</summary>
        /// <remarks>Similar to a System.Versioning.FrameworkName, but missing the v at the beginning of the version.</remarks>
        public string DotNetPlatformName
        {
            get
            {
                if (_targetPlatformMoniker == null)
                {
                    _targetPlatformMoniker = string.IsNullOrEmpty(Platform)
                        ? string.Empty
                        : Platform + ",Version=" + GetDisplayVersion(PlatformVersion);
                }

                return _targetPlatformMoniker;
            }
        }

        /// <summary>
        /// Helper that is .NET 5 Era aware to replace identifier when appropriate
        /// </summary>
        private string GetFrameworkIdentifier()
        {
            return IsNet5Era ? FrameworkConstants.FrameworkIdentifiers.Net : Framework;
        }

        /// <summary>
        /// Creates the shortened version of the framework using the given mappings.
        /// </summary>
        public virtual string GetShortFolderName(IFrameworkNameProvider mappings)
        {
            // Check for rewrites
            var framework = mappings.GetShortNameReplacement(this);

            var sb = StringBuilderPool.Shared.Rent(256);

            if (IsSpecificFramework)
            {
                var shortFramework = string.Empty;

                // get the framework
                if (!mappings.TryGetShortIdentifier(
                    GetFrameworkIdentifier(),
                    out shortFramework))
                {
                    shortFramework = GetLettersAndDigitsOnly(framework.Framework);
                }

                if (string.IsNullOrEmpty(shortFramework))
                {
                    throw new FrameworkException(string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.InvalidFrameworkIdentifier,
                        shortFramework));
                }

                // add framework
                sb.Append(shortFramework);

                // add the version if it is non-empty
                if (!AllFrameworkVersions)
                {
                    sb.Append(mappings.GetVersionString(framework.Framework, framework.Version));
                }

                if (IsPCL)
                {
                    sb.Append("-");

                    if (framework.HasProfile
                        && mappings.TryGetPortableFrameworks(framework.Profile, includeOptional: false, out IEnumerable<NuGetFramework>? frameworks)
                        && frameworks.Any())
                    {
                        var required = new HashSet<NuGetFramework>(frameworks, Comparer);

                        // Normalize by removing all optional frameworks
                        mappings.TryGetPortableFrameworks(framework.Profile, includeOptional: false, out frameworks);

                        // sort the PCL frameworks by alphabetical order
                        var sortedFrameworks = required.Select(e => e.GetShortFolderName(mappings)).OrderBy(e => e, StringComparer.OrdinalIgnoreCase);

                        sb.Append(string.Join("+", sortedFrameworks));
                    }
                    else
                    {
                        throw new FrameworkException(string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.MissingPortableFrameworks,
                            framework.DotNetFrameworkName));
                    }
                }
                else if (IsNet5Era)
                {
                    if (!string.IsNullOrEmpty(framework.Platform))
                    {
                        sb.Append("-");
                        sb.Append(framework.Platform.ToLowerInvariant());

                        if (framework.PlatformVersion != FrameworkConstants.EmptyVersion)
                        {
                            sb.Append(mappings.GetVersionString(framework.Framework, framework.PlatformVersion));
                        }
                    }
                }
                else
                {
                    // add the profile
                    var shortProfile = string.Empty;

                    if (framework.HasProfile && !mappings.TryGetShortProfile(framework.Framework, framework.Profile, out shortProfile))
                    {
                        // if we have a profile, but can't get a mapping, just use the profile as is
                        shortProfile = framework.Profile;
                    }

                    if (!string.IsNullOrEmpty(shortProfile))
                    {
                        sb.Append("-");
                        sb.Append(shortProfile);
                    }
                }
            }
            else
            {
                // unsupported, any, agnostic
                sb.Append(Framework);
            }

            return StringBuilderPool.Shared.ToStringAndReturn(sb).ToLowerInvariant();
        }

        private static string GetDisplayVersion(Version version)
        {
            var sb = StringBuilderPool.Shared.Rent(256);
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);

            if (version.Build > 0
                || version.Revision > 0)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, ".{0}", version.Build);

                if (version.Revision > 0)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, ".{0}", version.Revision);
                }
            }

            return StringBuilderPool.Shared.ToStringAndReturn(sb);
        }

        private static string GetLettersAndDigitsOnly(string s)
        {
            var sb = new StringBuilder();

            foreach (var c in s.ToCharArray())
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Portable class library check
        /// </summary>
        public bool IsPCL
        {
            get { return StringComparer.OrdinalIgnoreCase.Equals(Framework, FrameworkConstants.FrameworkIdentifiers.Portable) && Version.Major < 5; }
        }

        /// <summary>
        /// True if the framework is packages based.
        /// Ex: dotnet, dnxcore, netcoreapp, netstandard, uap, netcore50
        /// </summary>
        public bool IsPackageBased
        {
            get
            {
                // For these frameworks all versions are packages based.
                if (PackagesBased.Contains(Framework))
                {
                    return true;
                }

                // NetCore 5.0 and up are packages based.
                // Everything else is not packages based.
                return NuGetFrameworkUtility.IsNetCore50AndUp(this);
            }
        }

        /// <summary>
        /// True if this framework matches for all versions.
        /// Ex: net
        /// </summary>
        public bool AllFrameworkVersions
        {
            get { return Version.Major == 0 && Version.Minor == 0 && Version.Build == 0 && Version.Revision == 0; }
        }

        /// <summary>
        /// True if this framework was invalid or unknown. This framework is only compatible with Any and Agnostic.
        /// </summary>
        public bool IsUnsupported
        {
            get { return UnsupportedFramework.Equals(this); }
        }

        /// <summary>
        /// True if this framework is non-specific. Always compatible.
        /// </summary>
        public bool IsAgnostic
        {
            get { return AgnosticFramework.Equals(this); }
        }

        /// <summary>
        /// True if this is the any framework. Always compatible.
        /// </summary>
        public bool IsAny
        {
            get { return AnyFramework.Equals(this); }
        }

        /// <summary>
        /// True if this framework is real and not one of the special identifiers.
        /// </summary>
        public bool IsSpecificFramework
        {
            get { return !IsAgnostic && !IsAny && !IsUnsupported; }
        }

        /// <summary>
        /// True if this framework is Net5 or later, until we invent something new.
        /// </summary>
        internal bool IsNet5Era { get; private set; }

        /// <summary>
        /// Full framework comparison of the identifier, version, profile, platform, and platform version
        /// </summary>
        public static readonly IEqualityComparer<NuGetFramework> Comparer = NuGetFrameworkFullComparer.Instance;

        /// <summary>
        /// Framework name only comparison.
        /// </summary>
        public static readonly IEqualityComparer<NuGetFramework> FrameworkNameComparer = NuGetFrameworkNameComparer.Instance;

        public bool Equals(NuGetFramework? other)
        {
#pragma warning disable CS8604 // Possible null reference argument.
            // Nullable annotations were added to the BCL for IEqualityComparer in .NET 5
            return Comparer.Equals(this, other);
#pragma warning restore CS8604 // Possible null reference argument.
        }

        public static bool operator ==(NuGetFramework? left, NuGetFramework? right)
        {
#pragma warning disable CS8604 // Possible null reference argument.
            // Nullable annotations were added to the BCL for IEqualityComparer in .NET 5
            return Comparer.Equals(left, right);
#pragma warning restore CS8604 // Possible null reference argument.
        }

        public static bool operator !=(NuGetFramework? left, NuGetFramework? right)
        {
            return !(left == right);
        }

        public override int GetHashCode()
        {
            if (_hashCode == null)
            {
                _hashCode = Comparer.GetHashCode(this);
            }

            return _hashCode.Value;
        }

        public override bool Equals(object? obj)
        {
            var other = obj as NuGetFramework;

            if (other != null)
            {
                return Equals(other);
            }
            else
            {
                return base.Equals(obj);
            }
        }

        private static Version NormalizeVersion(Version version)
        {
            var normalized = version;

            if (version.Build < 0
                || version.Revision < 0)
            {
                normalized = new Version(
                    version.Major,
                    version.Minor,
                    Math.Max(version.Build, 0),
                    Math.Max(version.Revision, 0));
            }

            return normalized;
        }

        /// <summary>
        /// Frameworks that are packages based across all versions.
        /// </summary>
        private static readonly SortedSet<string> PackagesBased = new SortedSet<string>(
            new[]
            {
                        FrameworkConstants.FrameworkIdentifiers.DnxCore,
                        FrameworkConstants.FrameworkIdentifiers.NetPlatform,
                        FrameworkConstants.FrameworkIdentifiers.NetStandard,
                        FrameworkConstants.FrameworkIdentifiers.NetStandardApp,
                        FrameworkConstants.FrameworkIdentifiers.NetCoreApp,
                        FrameworkConstants.FrameworkIdentifiers.UAP,
                        FrameworkConstants.FrameworkIdentifiers.Tizen,
            },
            StringComparer.OrdinalIgnoreCase);
    }
    internal static class FrameworkConstants
    {
        public static readonly Version EmptyVersion = new Version(0, 0, 0, 0);
        public static readonly Version MaxVersion = new Version(int.MaxValue, 0, 0, 0);
        public static readonly Version Version5 = new Version(5, 0, 0, 0);
        public static readonly Version Version6 = new Version(6, 0, 0, 0);
        public static readonly Version Version7 = new Version(7, 0, 0, 0);
        public static readonly Version Version8 = new Version(8, 0, 0, 0);
        public static readonly Version Version9 = new Version(9, 0, 0, 0);
        public static readonly Version Version10 = new Version(10, 0, 0, 0);

        public static class SpecialIdentifiers
        {
            public const string Any = "Any";
            public const string Agnostic = "Agnostic";
            public const string Unsupported = "Unsupported";
        }

        public static class PlatformIdentifiers
        {
            public const string WindowsPhone = "WindowsPhone";
            public const string Windows = "Windows";
        }

        public static class FrameworkIdentifiers
        {
            public const string NetCoreApp = ".NETCoreApp";
            public const string NetStandardApp = ".NETStandardApp";
            public const string NetStandard = ".NETStandard";
            public const string NetPlatform = ".NETPlatform";
            public const string DotNet = "dotnet";
            public const string Net = ".NETFramework";
            public const string NetCore = ".NETCore";
            public const string WinRT = "WinRT"; // deprecated
            public const string NetMicro = ".NETMicroFramework";
            public const string Portable = ".NETPortable";
            public const string WindowsPhone = "WindowsPhone";
            public const string Windows = "Windows";
            public const string WindowsPhoneApp = "WindowsPhoneApp";
            public const string Dnx = "DNX";
            public const string DnxCore = "DNXCore";
            public const string AspNet = "ASP.NET";
            public const string AspNetCore = "ASP.NETCore";
            public const string Silverlight = "Silverlight";
            public const string Native = "native";
            public const string MonoAndroid = "MonoAndroid";
            public const string MonoTouch = "MonoTouch";
            public const string MonoMac = "MonoMac";
            public const string XamarinIOs = "Xamarin.iOS";
            public const string XamarinMac = "Xamarin.Mac";
            public const string XamarinPlayStation3 = "Xamarin.PlayStation3";
            public const string XamarinPlayStation4 = "Xamarin.PlayStation4";
            public const string XamarinPlayStationVita = "Xamarin.PlayStationVita";
            public const string XamarinWatchOS = "Xamarin.WatchOS";
            public const string XamarinTVOS = "Xamarin.TVOS";
            public const string XamarinXbox360 = "Xamarin.Xbox360";
            public const string XamarinXboxOne = "Xamarin.XboxOne";
            public const string UAP = "UAP";
            public const string Tizen = "Tizen";
            public const string NanoFramework = ".NETnanoFramework";
        }

        /// <summary>
        /// Interned frameworks that are commonly used in NuGet
        /// </summary>
        internal static class CommonFrameworks
        {
            /// <summary>net11 (.NETFramework,Version=v1.1)</summary>
            public static readonly NuGetFramework Net11 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(1, 1, 0, 0));
            /// <summary>net20 (.NETFramework,Version=v2.0)</summary>
            public static readonly NuGetFramework Net2 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(2, 0, 0, 0));
            /// <summary>net35 (.NETFramework,Version=v3.5)</summary>
            public static readonly NuGetFramework Net35 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(3, 5, 0, 0));
            /// <summary>net40 (.NETFramework,Version=v4.0)</summary>
            public static readonly NuGetFramework Net4 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 0, 0, 0));
            /// <summary>net403 (.NETFramework,Version=v4.0.3)</summary>
            public static readonly NuGetFramework Net403 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 0, 3, 0));
            /// <summary>net45 (.NETFramework,Version=v4.5)</summary>
            public static readonly NuGetFramework Net45 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 5, 0, 0));
            /// <summary>net451 (.NETFramework,Version=v4.5.1)</summary>
            public static readonly NuGetFramework Net451 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 5, 1, 0));
            /// <summary>net452 (.NETFramework,Version=v4.5.2)</summary>
            public static readonly NuGetFramework Net452 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 5, 2, 0));
            /// <summary>net46 (.NETFramework,Version=v4.6)</summary>
            public static readonly NuGetFramework Net46 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 6, 0, 0));
            /// <summary>net461 (.NETFramework,Version=v4.6.1)</summary>
            public static readonly NuGetFramework Net461 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 6, 1, 0));
            /// <summary>net462 (.NETFramework,Version=v4.6.2)</summary>
            public static readonly NuGetFramework Net462 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 6, 2, 0));
            /// <summary>net463 (.NETFramework,Version=v4.6.3)</summary>
            public static readonly NuGetFramework Net463 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 6, 3, 0));
            /// <summary>net47 (.NETFramework,Version=v4.7)</summary>
            public static readonly NuGetFramework Net47 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 7, 0, 0));
            /// <summary>net471 (.NETFramework,Version=v4.7.1)</summary>
            public static readonly NuGetFramework Net471 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 7, 1, 0));
            /// <summary>net472 (.NETFramework,Version=v4.7.2)</summary>
            public static readonly NuGetFramework Net472 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 7, 2, 0));
            /// <summary>net48 (.NETFramework,Version=v4.8)</summary>
            public static readonly NuGetFramework Net48 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 8, 0, 0));
            /// <summary>net481 (.NETFramework,Version=v4.8.1)</summary>
            public static readonly NuGetFramework Net481 = new NuGetFramework(FrameworkIdentifiers.Net, new Version(4, 8, 1, 0));

            /// <summary>netcore45 (.NETCore,Version=v4.5)</summary>
            /// <remarks>This is not .NET Core. You are probably looking for netcoreapp.</remarks>
            public static readonly NuGetFramework NetCore45 = new NuGetFramework(FrameworkIdentifiers.NetCore, new Version(4, 5, 0, 0));
            /// <summary>netcore451 (.NETCore,Version=v4.5.1)</summary>
            /// <remarks>This is not .NET Core. You are probably looking for netcoreapp.</remarks>
            public static readonly NuGetFramework NetCore451 = new NuGetFramework(FrameworkIdentifiers.NetCore, new Version(4, 5, 1, 0));
            /// <summary>netcore50 (.NETCore,Version=v5.0)</summary>
            /// <remarks>This is not .NET 5. You are probably looking for net50 (.NETCoreApp,Version=v5.0)</remarks>
            public static readonly NuGetFramework NetCore50 = new NuGetFramework(FrameworkIdentifiers.NetCore, new Version(5, 0, 0, 0));

            public static readonly NuGetFramework Win8 = new NuGetFramework(FrameworkIdentifiers.Windows, new Version(8, 0, 0, 0));
            public static readonly NuGetFramework Win81 = new NuGetFramework(FrameworkIdentifiers.Windows, new Version(8, 1, 0, 0));
            public static readonly NuGetFramework Win10 = new NuGetFramework(FrameworkIdentifiers.Windows, new Version(10, 0, 0, 0));

            public static readonly NuGetFramework SL4 = new NuGetFramework(FrameworkIdentifiers.Silverlight, new Version(4, 0, 0, 0));
            public static readonly NuGetFramework SL5 = new NuGetFramework(FrameworkIdentifiers.Silverlight, new Version(5, 0, 0, 0));

            public static readonly NuGetFramework WP7 = new NuGetFramework(FrameworkIdentifiers.WindowsPhone, new Version(7, 0, 0, 0));
            public static readonly NuGetFramework WP75 = new NuGetFramework(FrameworkIdentifiers.WindowsPhone, new Version(7, 5, 0, 0));
            public static readonly NuGetFramework WP8 = new NuGetFramework(FrameworkIdentifiers.WindowsPhone, new Version(8, 0, 0, 0));
            public static readonly NuGetFramework WP81 = new NuGetFramework(FrameworkIdentifiers.WindowsPhone, new Version(8, 1, 0, 0));
            public static readonly NuGetFramework WPA81 = new NuGetFramework(FrameworkIdentifiers.WindowsPhoneApp, new Version(8, 1, 0, 0));

            public static readonly NuGetFramework Tizen3 = new NuGetFramework(FrameworkIdentifiers.Tizen, new Version(3, 0, 0, 0));
            public static readonly NuGetFramework Tizen4 = new NuGetFramework(FrameworkIdentifiers.Tizen, new Version(4, 0, 0, 0));
            public static readonly NuGetFramework Tizen6 = new NuGetFramework(FrameworkIdentifiers.Tizen, new Version(6, 0, 0, 0));

            public static readonly NuGetFramework AspNet = new NuGetFramework(FrameworkIdentifiers.AspNet, EmptyVersion);
            public static readonly NuGetFramework AspNetCore = new NuGetFramework(FrameworkIdentifiers.AspNetCore, EmptyVersion);
            public static readonly NuGetFramework AspNet50 = new NuGetFramework(FrameworkIdentifiers.AspNet, Version5);
            public static readonly NuGetFramework AspNetCore50 = new NuGetFramework(FrameworkIdentifiers.AspNetCore, Version5);

            public static readonly NuGetFramework Dnx = new NuGetFramework(FrameworkIdentifiers.Dnx, EmptyVersion);
            public static readonly NuGetFramework Dnx45 = new NuGetFramework(FrameworkIdentifiers.Dnx, new Version(4, 5, 0, 0));
            public static readonly NuGetFramework Dnx451 = new NuGetFramework(FrameworkIdentifiers.Dnx, new Version(4, 5, 1, 0));
            public static readonly NuGetFramework Dnx452 = new NuGetFramework(FrameworkIdentifiers.Dnx, new Version(4, 5, 2, 0));
            public static readonly NuGetFramework DnxCore = new NuGetFramework(FrameworkIdentifiers.DnxCore, EmptyVersion);
            public static readonly NuGetFramework DnxCore50 = new NuGetFramework(FrameworkIdentifiers.DnxCore, Version5);

            public static readonly NuGetFramework DotNet
                = new NuGetFramework(FrameworkIdentifiers.NetPlatform, EmptyVersion);
            public static readonly NuGetFramework DotNet50
                = new NuGetFramework(FrameworkIdentifiers.NetPlatform, Version5);
            public static readonly NuGetFramework DotNet51
                = new NuGetFramework(FrameworkIdentifiers.NetPlatform, new Version(5, 1, 0, 0));
            public static readonly NuGetFramework DotNet52
                = new NuGetFramework(FrameworkIdentifiers.NetPlatform, new Version(5, 2, 0, 0));
            public static readonly NuGetFramework DotNet53
                = new NuGetFramework(FrameworkIdentifiers.NetPlatform, new Version(5, 3, 0, 0));
            public static readonly NuGetFramework DotNet54
                = new NuGetFramework(FrameworkIdentifiers.NetPlatform, new Version(5, 4, 0, 0));
            public static readonly NuGetFramework DotNet55
                = new NuGetFramework(FrameworkIdentifiers.NetPlatform, new Version(5, 5, 0, 0));
            public static readonly NuGetFramework DotNet56
                = new NuGetFramework(FrameworkIdentifiers.NetPlatform, new Version(5, 6, 0, 0));

            public static readonly NuGetFramework NetStandard
                = new NuGetFramework(FrameworkIdentifiers.NetStandard, EmptyVersion);
            /// <summary>netstandard1.0 (.NETStandard,Version=v1.0)</summary>
            public static readonly NuGetFramework NetStandard10
                = new NuGetFramework(FrameworkIdentifiers.NetStandard, new Version(1, 0, 0, 0));
            /// <summary>netstandard1.1 (.NETStandard,Version=v1.1)</summary>
            public static readonly NuGetFramework NetStandard11
                = new NuGetFramework(FrameworkIdentifiers.NetStandard, new Version(1, 1, 0, 0));
            /// <summary>netstandard1.2 (.NETStandard,Version=v1.2)</summary>
            public static readonly NuGetFramework NetStandard12
                = new NuGetFramework(FrameworkIdentifiers.NetStandard, new Version(1, 2, 0, 0));
            /// <summary>netstandard1.3 (.NETStandard,Version=v1.3)</summary>
            public static readonly NuGetFramework NetStandard13
                = new NuGetFramework(FrameworkIdentifiers.NetStandard, new Version(1, 3, 0, 0));
            /// <summary>netstandard1.4 (.NETStandard,Version=v1.4)</summary>
            public static readonly NuGetFramework NetStandard14
                = new NuGetFramework(FrameworkIdentifiers.NetStandard, new Version(1, 4, 0, 0));
            /// <summary>netstandard1.5 (.NETStandard,Version=v1.5)</summary>
            public static readonly NuGetFramework NetStandard15
                = new NuGetFramework(FrameworkIdentifiers.NetStandard, new Version(1, 5, 0, 0));
            /// <summary>netstandard1.6 (.NETStandard,Version=v1.6)</summary>
            public static readonly NuGetFramework NetStandard16
                = new NuGetFramework(FrameworkIdentifiers.NetStandard, new Version(1, 6, 0, 0));
            /// <summary>netstandard1.7 (.NETStandard,Version=v1.7</summary>
            public static readonly NuGetFramework NetStandard17
                = new NuGetFramework(FrameworkIdentifiers.NetStandard, new Version(1, 7, 0, 0));
            /// <summary>netstandard2.0 (.NETStandard,Version=v2.0)</summary>
            public static readonly NuGetFramework NetStandard20
                = new NuGetFramework(FrameworkIdentifiers.NetStandard, new Version(2, 0, 0, 0));
            /// <summary>netstandard2.1 (.NETStandard,Version=v2.1)</summary>
            public static readonly NuGetFramework NetStandard21
                = new NuGetFramework(FrameworkIdentifiers.NetStandard, new Version(2, 1, 0, 0));

            public static readonly NuGetFramework NetStandardApp15
                = new NuGetFramework(FrameworkIdentifiers.NetStandardApp, new Version(1, 5, 0, 0));

            public static readonly NuGetFramework UAP10
                = new NuGetFramework(FrameworkIdentifiers.UAP, Version10);

            /// <summary>netcoreapp1.0 (.NETCoreApp,Version=v1.0)</summary>
            public static readonly NuGetFramework NetCoreApp10
                = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, new Version(1, 0, 0, 0));
            /// <summary>netcoreapp1.1 (.NETCoreApp,Version=v1.1)</summary>
            public static readonly NuGetFramework NetCoreApp11
                = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, new Version(1, 1, 0, 0));
            /// <summary>netcoreapp2.0 (.NETCoreApp,Version=v2.0)</summary>
            public static readonly NuGetFramework NetCoreApp20
                = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, new Version(2, 0, 0, 0));
            /// <summary>netcoreapp2.1 (.NETCoreApp,Version=v2.1)</summary>
            public static readonly NuGetFramework NetCoreApp21
                = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, new Version(2, 1, 0, 0));
            /// <summary>netcoreapp2.2 (.NETCoreApp,Version=v2.2)</summary>
            public static readonly NuGetFramework NetCoreApp22
                = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, new Version(2, 2, 0, 0));
            /// <summary>netcoreapp3.0 (.NETCoreApp,Version=v3.0)</summary>
            public static readonly NuGetFramework NetCoreApp30
                = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, new Version(3, 0, 0, 0));
            /// <summary>netcoreapp3.1 (.NETCoreApp,Version=v3.1)</summary>
            public static readonly NuGetFramework NetCoreApp31
                = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, new Version(3, 1, 0, 0));

            // .NET 5.0 and later has NetCoreApp identifier
            /// <summary>net5.0 (.NETCoreApp,Version=v5.0)</summary>
            public static readonly NuGetFramework Net50 = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, Version5);
            /// <summary>net6.0 (.NETCoreApp,Version=v6.0)</summary>
            public static readonly NuGetFramework Net60 = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, Version6);
            /// <summary>net7.0 (.NETCoreApp,Version=v7.0)</summary>
            public static readonly NuGetFramework Net70 = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, Version7);
            /// <summary>net8.0 (.NETCoreApp,Version=v8.0)</summary>
            public static readonly NuGetFramework Net80 = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, Version8);
            /// <summary>net9.0 (.NETCoreApp,Version=v9.0)</summary>
            public static readonly NuGetFramework Net90 = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, Version9);
            /// <summary>net10.0 (.NETCoreApp,Version=v10.0)</summary>
            public static readonly NuGetFramework Net10_0 = new NuGetFramework(FrameworkIdentifiers.NetCoreApp, Version10);

            public static readonly NuGetFramework Native = new NuGetFramework(FrameworkIdentifiers.Native, new Version(0, 0, 0, 0));
        }

        internal interface IFrameworkNameProvider
        {
            /// <summary>
            /// Returns the official framework identifier for an alias or short name.
            /// </summary>
            bool TryGetIdentifier(string identifierShortName, [NotNullWhen(true)] out string? identifier);

            /// <summary>
            /// Gives the short name used for folders in NuGet
            /// </summary>
            bool TryGetShortIdentifier(string identifier, [NotNullWhen(true)] out string? identifierShortName);

            /// <summary>
            /// Get the official profile name from the short name.
            /// </summary>
            bool TryGetProfile(string frameworkIdentifier, string profileShortName, [NotNullWhen(true)] out string? profile);

            /// <summary>
            /// Returns the shortened version of the profile name.
            /// </summary>
            bool TryGetShortProfile(string frameworkIdentifier, string profile, [NotNullWhen(true)] out string? profileShortName);

            /// <summary>
            /// Parses a version string using single digit rules if no dots exist
            /// </summary>
            bool TryGetVersion(string versionString, [NotNullWhen(true)] out Version? version);

            /// <summary>
            /// Parses a version string. If no dots exist, all digits are treated
            /// as semver-major, instead of inserting dots.
            /// </summary>
            bool TryGetPlatformVersion(string versionString, [NotNullWhen(true)] out Version? version);

            /// <summary>
            /// Returns a shortened version. If all digits are single digits no dots will be used.
            /// </summary>
            string GetVersionString(string framework, Version version);

            /// <summary>
            /// Tries to parse the portable profile number out of a profile.
            /// </summary>
            bool TryGetPortableProfileNumber(string profile, out int profileNumber);

            /// <summary>
            /// Looks up the portable profile number based on the framework list.
            /// </summary>
            bool TryGetPortableProfile(IEnumerable<NuGetFramework> supportedFrameworks, out int profileNumber);

            /// <summary>
            /// Returns the frameworks based on a portable profile number.
            /// </summary>
            bool TryGetPortableFrameworks(int profile, [NotNullWhen(true)] out IEnumerable<NuGetFramework>? frameworks);

            /// <summary>
            /// Returns the frameworks based on a portable profile number.
            /// </summary>
            bool TryGetPortableFrameworks(int profile, bool includeOptional, [NotNullWhen(true)] out IEnumerable<NuGetFramework>? frameworks);

            /// <summary>
            /// Returns the frameworks based on a profile string.
            /// Profile can be either the number in format: Profile=7, or the shortened NuGet version: net45+win8
            /// </summary>
            bool TryGetPortableFrameworks(string profile, bool includeOptional, [NotNullWhen(true)] out IEnumerable<NuGetFramework>? frameworks);

            /// <summary>
            /// Parses a shortened portable framework profile list.
            /// Ex: net45+win8
            /// </summary>
            bool TryGetPortableFrameworks(string shortPortableProfiles, [NotNullWhen(true)] out IEnumerable<NuGetFramework>? frameworks);

            /// <summary>
            /// Returns ranges of frameworks that are known to be supported by the given portable profile number.
            /// Ex: Profile7 -> netstandard1.1
            /// </summary>
            bool TryGetPortableCompatibilityMappings(int profile, [NotNullWhen(true)] out IEnumerable<FrameworkRange>? supportedFrameworkRanges);

            /// <summary>
            /// Returns a list of all possible substitutions where the framework name
            /// have equivalents.
            /// Ex: sl3 -> wp8
            /// </summary>
            bool TryGetEquivalentFrameworks(NuGetFramework framework, [NotNullWhen(true)] out IEnumerable<NuGetFramework>? frameworks);

            /// <summary>
            /// Gives all substitutions for a framework range.
            /// </summary>
            bool TryGetEquivalentFrameworks(FrameworkRange range, [NotNullWhen(true)] out IEnumerable<NuGetFramework>? frameworks);

            /// <summary>
            /// Returns ranges of frameworks that are known to be supported by the given framework.
            /// Ex: net45 -> native
            /// </summary>
            bool TryGetCompatibilityMappings(NuGetFramework framework, [NotNullWhen(true)] out IEnumerable<FrameworkRange>? supportedFrameworkRanges);

            /// <summary>
            /// Returns all sub sets of the given framework.
            /// Ex: .NETFramework -> .NETCore
            /// These will have the same version, but a different framework
            /// </summary>
            bool TryGetSubSetFrameworks(string frameworkIdentifier, [NotNullWhen(true)] out IEnumerable<string>? subSetFrameworkIdentifiers);

            /// <summary>
            /// The ascending order of frameworks should be based on the following ordered groups:
            /// 
            /// 1. Non-package-based frameworks in <see cref="IFrameworkMappings.NonPackageBasedFrameworkPrecedence"/>.
            /// 2. Other non-package-based frameworks.
            /// 3. Package-based frameworks in <see cref="IFrameworkMappings.PackageBasedFrameworkPrecedence"/>.
            /// 4. Other package-based frameworks.
            /// 
            /// For group #1 and #3, the order within the group is based on the order of the respective precedence list.
            /// For group #2 and #4, the order is the original order in the incoming list. This should later be made
            /// consistent between different input orderings by using the <see cref="NuGetFrameworkSorter"/>.
            /// </summary>
            /// <remarks>netcore50 is a special case since netcore451 is not packages based, but netcore50 is.
            /// This sort will treat all versions of netcore as non-packages based.</remarks>
            int CompareFrameworks(NuGetFramework? x, NuGetFramework? y);

            /// <summary>
            /// Used to pick between two equivalent frameworks. This is meant to favor the more human-readable
            /// framework. Note that this comparison does not validate that the provided frameworks are indeed
            /// equivalent (e.g. with
            /// <see cref="TryGetEquivalentFrameworks(NuGetFramework, out IEnumerable{NuGetFramework})"/>).
            /// </summary>
            int CompareEquivalentFrameworks(NuGetFramework? x, NuGetFramework? y);

            /// <summary>
            /// Returns folder short names rewrites.
            /// Ex: dotnet50 -> dotnet
            /// </summary>
            NuGetFramework GetShortNameReplacement(NuGetFramework framework);

            /// <summary>
            /// Returns full name rewrites.
            /// Ex: .NETPlatform,Version=v0.0 -> .NETPlatform,Version=v5.0
            /// </summary>
            NuGetFramework GetFullNameReplacement(NuGetFramework framework);

            /// <summary>
            /// Returns all versions of .NETStandard in ascending order.
            /// </summary>
            IEnumerable<NuGetFramework> GetNetStandardVersions();

            /// <summary>
            /// Returns a list of frameworks that could be compatible with .NETStandard.
            /// </summary>
            IEnumerable<NuGetFramework> GetCompatibleCandidates();
        }

        internal class FrameworkNameProvider : IFrameworkNameProvider
        {
            private static readonly HashSet<NuGetFramework> EmptyFrameworkSet = new();

            /// <summary>
            /// Legacy frameworks that are allowed to have a single digit for the version number.
            /// </summary>
            private static readonly HashSet<string> SingleDigitVersionFrameworks = new(StringComparer.OrdinalIgnoreCase)
        {
            FrameworkConstants.FrameworkIdentifiers.Windows,
            FrameworkConstants.FrameworkIdentifiers.WindowsPhone,
            FrameworkConstants.FrameworkIdentifiers.Silverlight
        };

            /// <summary>
            /// Frameworks that must always include a decimal point (period) between numerical parts.
            /// </summary>
            private static readonly HashSet<string> DecimalPointFrameworks = new(StringComparer.OrdinalIgnoreCase)
        {
            FrameworkConstants.FrameworkIdentifiers.NetCoreApp,
            FrameworkConstants.FrameworkIdentifiers.NetStandard,
            FrameworkConstants.FrameworkIdentifiers.NanoFramework
        };

            /// <summary>
            /// Contains identifier -> identifier
            /// Ex: .NET Framework -> .NET Framework
            /// Ex: NET Framework -> .NET Framework
            /// This includes self mappings.
            /// </summary>
            private readonly Dictionary<string, string> _identifierSynonyms;

            private readonly Dictionary<string, string> _identifierToShortName;
            private readonly Dictionary<string, string> _profilesToShortName;
            private readonly Dictionary<string, string> _identifierShortToLong;
            private readonly Dictionary<string, string> _profileShortToLong;

            // profile -> supported frameworks, optional frameworks
            private readonly Dictionary<int, HashSet<NuGetFramework>> _portableFrameworks;
            private readonly Dictionary<int, HashSet<NuGetFramework>> _portableOptionalFrameworks;

            // PCL compatibility mappings
            private readonly Dictionary<int, HashSet<FrameworkRange>> _portableCompatibilityMappings;

            // equivalent frameworks
            private readonly Dictionary<NuGetFramework, HashSet<NuGetFramework>> _equivalentFrameworks;

            // equivalent profiles
            private readonly Dictionary<string, Dictionary<string, HashSet<string>>> _equivalentProfiles;

            // non-PCL compatibility mappings
            private readonly Dictionary<string, HashSet<OneWayCompatibilityMappingEntry>> _compatibilityMappings;

            // subsets, net -> netcore
            private readonly Dictionary<string, HashSet<string>> _subSetFrameworks;

            // framework ordering (for non-package based frameworks)
            private readonly Dictionary<string, int> _nonPackageBasedFrameworkPrecedence;

            // framework ordering (for package based frameworks)
            private readonly Dictionary<string, int> _packageBasedFrameworkPrecedence;

            // framework ordering (when choosing between equivalent frameworks)
            private readonly Dictionary<string, int> _equivalentFrameworkPrecedence;

            // Rewrite mappings
            private readonly Dictionary<NuGetFramework, NuGetFramework> _shortNameRewrites;
            private readonly Dictionary<NuGetFramework, NuGetFramework> _fullNameRewrites;

            // NetStandard information
            private readonly List<NuGetFramework> _netStandardVersions;
            private readonly List<NuGetFramework> _compatibleCandidates;

            public FrameworkNameProvider(IEnumerable<IFrameworkMappings>? mappings, IEnumerable<IPortableFrameworkMappings>? portableMappings)
            {
                _identifierSynonyms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _identifierToShortName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _profilesToShortName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _identifierShortToLong = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _profileShortToLong = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _portableFrameworks = new Dictionary<int, HashSet<NuGetFramework>>();
                _portableOptionalFrameworks = new Dictionary<int, HashSet<NuGetFramework>>();
                _equivalentFrameworks = new Dictionary<NuGetFramework, HashSet<NuGetFramework>>();
                _equivalentProfiles = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);
                _subSetFrameworks = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                _nonPackageBasedFrameworkPrecedence = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                _packageBasedFrameworkPrecedence = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                _equivalentFrameworkPrecedence = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                _compatibilityMappings = new Dictionary<string, HashSet<OneWayCompatibilityMappingEntry>>(StringComparer.OrdinalIgnoreCase);
                _portableCompatibilityMappings = new Dictionary<int, HashSet<FrameworkRange>>();
                _shortNameRewrites = new Dictionary<NuGetFramework, NuGetFramework>();
                _fullNameRewrites = new Dictionary<NuGetFramework, NuGetFramework>();
                _netStandardVersions = new List<NuGetFramework>();
                _compatibleCandidates = new List<NuGetFramework>();

                InitMappings(mappings);

                InitPortableMappings(portableMappings);

                InitNetStandard();
            }

            /// <summary>
            /// Converts a key using the mappings, or if the key is already converted, finds the normalized form.
            /// </summary>
            private static bool TryConvertOrNormalize(string key, IDictionary<string, string> mappings, IDictionary<string, string> reverse, [NotNullWhen(true)] out string? value)
            {
                if (mappings.TryGetValue(key, out value))
                {
                    return true;
                }
                else if (reverse.ContainsKey(key))
                {
                    foreach (var item in reverse.NoAllocEnumerate())
                    {
                        if (StringComparer.OrdinalIgnoreCase.Equals(item.Key, key))
                        {
                            value = item.Key;
                            return true;
                        }
                    }
                }

                value = null;
                return false;
            }

            public bool TryGetIdentifier(string framework, [NotNullWhen(true)] out string? identifier)
            {
                return TryConvertOrNormalize(framework, _identifierSynonyms, _identifierToShortName, out identifier);
            }

            public bool TryGetProfile(string frameworkIdentifier, string profileShortName, [NotNullWhen(true)] out string? profile)
            {
                return TryConvertOrNormalize(profileShortName, _profileShortToLong, _profilesToShortName, out profile);
            }

            public bool TryGetShortIdentifier(string identifier, [NotNullWhen(true)] out string? identifierShortName)
            {
                return TryConvertOrNormalize(identifier, _identifierToShortName, _identifierShortToLong, out identifierShortName);
            }

            public bool TryGetShortProfile(string frameworkIdentifier, string profile, [NotNullWhen(true)] out string? profileShortName)
            {
                return TryConvertOrNormalize(profile, _profilesToShortName, _profileShortToLong, out profileShortName);
            }

            public bool TryGetVersion(string versionString, [NotNullWhen(true)] out Version? version)
            {
                if (string.IsNullOrEmpty(versionString))
                {
                    version = null;
                    return false;
                }
                else
                {
                    if (versionString.IndexOf('.') > -1)
                    {
                        // parse the version as a normal dot delimited version
                        return Version.TryParse(versionString, out version);
                    }
                    else
                    {
                        // make sure we have at least 2 digits
                        if (versionString.Length < 2)
                        {
                            versionString += "0";
                        }

                        // take only the first 4 digits and add dots
                        // 451 -> 4.5.1
                        // 81233 -> 8123
                        return Version.TryParse(string.Join(".", versionString.ToCharArray().Take(4)), out version);
                    }
                }
            }

            public bool TryGetPlatformVersion(string versionString, [NotNullWhen(true)] out Version? version)
            {
                if (string.IsNullOrEmpty(versionString))
                {
                    version = null;
                    return false;
                }
                else
                {
                    if (versionString.IndexOf('.') < 0)
                    {
                        versionString += ".0";
                    }
                    return Version.TryParse(versionString, out version);
                }
            }

            public string GetVersionString(string framework, Version version)
            {
                if (version is null || IsZero(version))
                {
                    return string.Empty;
                }

                int major = version.Major > 0 ? version.Major : 0;
                int minor = version.Minor > 0 ? version.Minor : 0;
                int build = version.Build > 0 ? version.Build : 0;
                int revision = version.Revision > 0 ? version.Revision : 0;

                // Remove all trailing zeros beyond the minor version.
                int partCount = (minor == 0, build == 0, revision == 0) switch
                {
                    (true, true, true) => 1,
                    (false, true, true) => 2,
                    (_, false, true) => 3,
                    (_, _, false) => 4
                };

                // Only some legacy frameworks are allowed to have one part in their version.
                if (partCount == 1 && !SingleDigitVersionFrameworks.Contains(framework))
                {
                    partCount = 2;
                }

                StringBuilder sb = StringBuilderPool.Shared.Rent(256);

                // Some frameworks require a decimal point between parts.
                // If any part is greater than 9 (requiring multiple digits), we add decimal points.
                if (DecimalPointFrameworks.Contains(framework) || HasGreaterThanNinePart())
                {
                    // An additional zero is needed for decimals.
                    if (partCount == 1)
                        partCount = 2;

                    sb.AppendInt(major);
                    if (partCount > 1)
                        sb.Append('.').AppendInt(minor);
                    if (partCount > 2)
                        sb.Append('.').AppendInt(build);
                    if (partCount > 3)
                        sb.Append('.').AppendInt(revision);
                }
                else
                {
                    sb.AppendInt(major);
                    if (partCount > 1)
                        sb.AppendInt(minor);
                    if (partCount > 2)
                        sb.AppendInt(build);
                    if (partCount > 3)
                        sb.AppendInt(revision);
                }

                return StringBuilderPool.Shared.ToStringAndReturn(sb);

                bool HasGreaterThanNinePart()
                {
                    return major > 9 || minor > 9 || build > 9 || revision > 9;
                }

                static bool IsZero(Version version)
                {
                    // Build and Revision can be -1 when only major & minor are specified.
                    // Out of caution, check all values for zero or less.
                    return version.Major <= 0
                        && version.Minor <= 0
                        && version.Build <= 0
                        && version.Revision <= 0;
                }
            }

            public bool TryGetPortableProfile(IEnumerable<NuGetFramework> supportedFrameworks, out int profileNumber)
            {
                if (supportedFrameworks == null)
                {
                    throw new ArgumentNullException(nameof(supportedFrameworks));
                }

                profileNumber = -1;

                // Remove duplicate frameworks, ex: win+win8 -> win
                var profileFrameworks = RemoveDuplicateFramework(supportedFrameworks);

                var reduced = new HashSet<NuGetFramework>();
                foreach (var pair in _portableFrameworks)
                {
                    // to match the required set must be less than or the same count as the input
                    // if we knew which frameworks were optional in the input we could rule out the lesser ones also
                    if (pair.Value.Count <= profileFrameworks.Count)
                    {
                        foreach (var curFw in profileFrameworks)
                        {
                            var isOptional = false;

                            foreach (var optional in GetOptionalFrameworks(pair.Key))
                            {
                                // TODO: profile check? Is the version check correct here?
                                if (NuGetFramework.FrameworkNameComparer.Equals(optional, curFw)
                                    && StringComparer.OrdinalIgnoreCase.Equals(optional.Profile, curFw.Profile)
                                    && curFw.Version >= optional.Version)
                                {
                                    isOptional = true;
                                }
                            }

                            if (!isOptional)
                            {
                                reduced.Add(curFw);
                            }
                        }

                        // check all frameworks while taking into account equivalent variations
                        foreach (var permutation in GetEquivalentPermutations(pair.Value))
                        {
                            if (SetEquals(reduced, permutation))
                            {
                                // found a match
                                profileNumber = pair.Key;
                                return true;
                            }
                        }
                    }

                    reduced.Clear();
                }

                return false;
            }

            private HashSet<NuGetFramework> RemoveDuplicateFramework(IEnumerable<NuGetFramework> supportedFrameworks)
            {
                var result = new HashSet<NuGetFramework>();
                var existingFrameworks = new HashSet<NuGetFramework>();

                foreach (var framework in supportedFrameworks)
                {
                    if (!existingFrameworks.Contains(framework))
                    {
                        result.Add(framework);

                        // Add in the existing framework (included here) and all equivalent frameworks  
                        var equivalentFrameworks = GetAllEquivalentFrameworks(framework);

                        UnionWith(existingFrameworks, equivalentFrameworks);
                    }
                }

                return result;
            }

            /// <summary>  
            /// Get all equivalent frameworks including the given framework  
            /// </summary>  
            private HashSet<NuGetFramework> GetAllEquivalentFrameworks(NuGetFramework framework)
            {
                // Loop through the frameworks, all frameworks that are not in results yet   
                // will be added to toProcess to get the equivalent frameworks  
                var toProcess = new Stack<NuGetFramework>();
                var results = new HashSet<NuGetFramework>();

                toProcess.Push(framework);
                results.Add(framework);

                while (toProcess.Count > 0)
                {
                    var current = toProcess.Pop();

                    if (_equivalentFrameworks.TryGetValue(current, out HashSet<NuGetFramework>? currentEquivalent))
                    {
                        foreach (var equalFramework in currentEquivalent)
                        {
                            if (results.Add(equalFramework))
                            {
                                toProcess.Push(equalFramework);
                            }
                        }
                    }
                }

                return results;
            }

            // find all combinations that are equivalent
            // ex: net4+win8 <-> net4+netcore45
            private IEnumerable<HashSet<NuGetFramework>> GetEquivalentPermutations(HashSet<NuGetFramework> frameworks)
            {
                if (frameworks.Count > 0)
                {
                    NuGetFramework? current = null;
                    var remaining = frameworks.Count == 1 ? null : new HashSet<NuGetFramework>();

                    var isFirst = true;
                    foreach (var fw in frameworks)
                    {
                        if (isFirst)
                        {
                            current = fw;
                            isFirst = false;
                            continue;
                        }

                        remaining!.Add(fw);
                    }

                    HashSet<NuGetFramework> equalFrameworks;

                    // find all equivalent frameworks for the current one
                    if (_equivalentFrameworks.TryGetValue(current!, out HashSet<NuGetFramework>? curFrameworks))
                    {
                        equalFrameworks = new HashSet<NuGetFramework>(curFrameworks);
                    }
                    else
                    {
                        equalFrameworks = new HashSet<NuGetFramework>();
                    }

                    // include ourselves
                    equalFrameworks.Add(current!);

                    foreach (var fw in equalFrameworks)
                    {
                        if (remaining != null && remaining.Count > 0)
                        {
                            foreach (var result in GetEquivalentPermutations(remaining))
                            {
                                // work backwards adding the frameworks into the sets
                                result.Add(fw);
                                yield return result;
                            }
                        }
                        else
                        {
                            var singleFramework = new HashSet<NuGetFramework>();
                            singleFramework.Add(fw);
                            yield return singleFramework;
                        }
                    }
                }

                yield break;
            }

            private HashSet<NuGetFramework> GetOptionalFrameworks(int profile)
            {
                if (_portableOptionalFrameworks.TryGetValue(profile, out HashSet<NuGetFramework>? frameworks))
                {
                    return frameworks;
                }

                return EmptyFrameworkSet;
            }

            public bool TryGetPortableFrameworks(int profile, [NotNullWhen(true)] out IEnumerable<NuGetFramework>? frameworks)
            {
                return TryGetPortableFrameworks(profile, true, out frameworks);
            }

            public bool TryGetPortableFrameworks(int profile, bool includeOptional, [NotNullWhen(true)] out IEnumerable<NuGetFramework>? frameworks)
            {
                var result = new HashSet<NuGetFramework>();
                if (_portableFrameworks.TryGetValue(profile, out HashSet<NuGetFramework>? tmpFrameworks))
                {
                    foreach (var fw in tmpFrameworks)
                    {
                        result.Add(fw);
                    }
                }

                if (includeOptional)
                {
                    if (_portableOptionalFrameworks.TryGetValue(profile, out HashSet<NuGetFramework>? optional))
                    {
                        foreach (var fw in optional)
                        {
                            result.Add(fw);
                        }
                    }
                }

                frameworks = result;
                return result.Count > 0;
            }

            public bool TryGetPortableFrameworks(string shortPortableProfiles, [NotNullWhen(true)] out IEnumerable<NuGetFramework>? frameworks)
            {
                if (shortPortableProfiles == null)
                {
                    throw new ArgumentNullException(nameof(shortPortableProfiles));
                }

                var shortNames = shortPortableProfiles.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);

                var result = new List<NuGetFramework>();
                foreach (var name in shortNames)
                {
                    var framework = NuGetFramework.Parse(name, this);
                    if (framework.HasProfile)
                    {
                        // Frameworks within the portable profile are not allowed
                        // to have profiles themselves #1869
                        throw new ArgumentException(string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.InvalidPortableFrameworksDueToHyphen,
                            shortPortableProfiles));
                    }

                    result.Add(framework);
                }

                frameworks = result;
                return result.Count > 0;
            }

            public bool TryGetPortableCompatibilityMappings(int profile, [NotNullWhen(true)] out IEnumerable<FrameworkRange>? supportedFrameworkRanges)
            {
                if (_portableCompatibilityMappings.TryGetValue(profile, out HashSet<FrameworkRange>? entries))
                {
                    supportedFrameworkRanges = entries;
                    return supportedFrameworkRanges.Any();
                }

                supportedFrameworkRanges = null;
                return false;
            }

            public bool TryGetPortableProfileNumber(string profile, out int profileNumber)
            {
                // attempt to parse the profile for a number
                if (profile.StartsWith("Profile", StringComparison.OrdinalIgnoreCase))
                {
                    var trimmed = profile.Substring(7, profile.Length - 7);
                    return int.TryParse(trimmed, out profileNumber);
                }

                profileNumber = -1;
                return false;
            }

            public bool TryGetPortableFrameworks(string profile, bool includeOptional, [NotNullWhen(true)] out IEnumerable<NuGetFramework>? frameworks)
            {
                // attempt to parse the profile for a number
                int profileNum;
                if (TryGetPortableProfileNumber(profile, out profileNum))
                {
                    if (TryGetPortableFrameworks(profileNum, includeOptional, out frameworks))
                    {
                        return true;
                    }

                    frameworks = Enumerable.Empty<NuGetFramework>();
                    return false;
                }

                // treat the profile as a list of frameworks
                return TryGetPortableFrameworks(profile, out frameworks);
            }

            public bool TryGetEquivalentFrameworks(NuGetFramework framework, [NotNullWhen(true)] out IEnumerable<NuGetFramework>? frameworks)
            {
                var result = new HashSet<NuGetFramework>();

                // add in all framework aliases
                if (_equivalentFrameworks.TryGetValue(framework, out HashSet<NuGetFramework>? eqFrameworks))
                {
                    foreach (var eqFw in eqFrameworks)
                    {
                        result.Add(eqFw);
                    }
                }

                var baseFrameworks = new List<NuGetFramework>(result);
                baseFrameworks.Add(framework);

                // add in all profile aliases
                foreach (var fw in baseFrameworks)
                {
                    if (_equivalentProfiles.TryGetValue(fw.Framework, out Dictionary<string, HashSet<string>>? eqProfiles))
                    {
                        if (eqProfiles.TryGetValue(fw.Profile, out HashSet<string>? matchingProfiles))
                        {
                            foreach (var eqProfile in matchingProfiles)
                            {
                                result.Add(new NuGetFramework(fw.Framework, fw.Version, eqProfile));
                            }
                        }
                    }
                }

                // do not include the original framework
                result.Remove(framework);

                frameworks = result;
                return result.Count > 0;
            }

            public bool TryGetEquivalentFrameworks(FrameworkRange range, [NotNullWhen(true)] out IEnumerable<NuGetFramework>? frameworks)
            {
                if (range == null)
                {
                    throw new ArgumentNullException(nameof(range));
                }

                var relevant = new HashSet<NuGetFramework>();

                foreach (var framework in _equivalentFrameworks.Keys.Where(f => range.Satisfies(f)))
                {
                    relevant.Add(framework);
                }

                var results = new HashSet<NuGetFramework>();

                foreach (var framework in relevant)
                {
                    if (TryGetEquivalentFrameworks(framework, out IEnumerable<NuGetFramework>? values))
                    {
                        foreach (var val in values)
                        {
                            results.Add(val);
                        }
                    }
                }

                frameworks = results;
                return results.Count > 0;
            }

            private void InitMappings(IEnumerable<IFrameworkMappings>? mappings)
            {
                if (mappings != null)
                {
                    foreach (var mapping in mappings)
                    {
                        // eq profiles
                        AddEquivalentProfiles(mapping.EquivalentProfiles);

                        // equivalent frameworks
                        AddEquivalentFrameworks(mapping.EquivalentFrameworks);

                        // add synonyms
                        AddFrameworkSynonyms(mapping.IdentifierSynonyms);

                        // populate short <-> long
                        AddIdentifierShortNames(mapping.IdentifierShortNames);

                        // official profile short names
                        AddProfileShortNames(mapping.ProfileShortNames);

                        // add compatibility mappings
                        AddCompatibilityMappings(mapping.CompatibilityMappings);

                        // add subset frameworks
                        AddSubSetFrameworks(mapping.SubSetFrameworks);

                        // add framework ordering rules
                        AddFrameworkPrecedenceMappings(_nonPackageBasedFrameworkPrecedence, mapping.NonPackageBasedFrameworkPrecedence);
                        AddFrameworkPrecedenceMappings(_packageBasedFrameworkPrecedence, mapping.PackageBasedFrameworkPrecedence);
                        AddFrameworkPrecedenceMappings(_equivalentFrameworkPrecedence, mapping.EquivalentFrameworkPrecedence);

                        // add rewrite rules
                        AddShortNameRewriteMappings(mapping.ShortNameReplacements);
                        AddFullNameRewriteMappings(mapping.FullNameReplacements);
                    }
                }
            }

            private void InitPortableMappings(IEnumerable<IPortableFrameworkMappings>? portableMappings)
            {
                if (portableMappings != null)
                {
                    foreach (var portableMapping in portableMappings)
                    {
                        // populate portable framework names
                        AddPortableProfileMappings(portableMapping.ProfileFrameworks);

                        // populate portable optional frameworks
                        AddPortableOptionalFrameworks(portableMapping.ProfileOptionalFrameworks);

                        // populate portable compatibility mappings
                        AddPortableCompatibilityMappings(portableMapping.CompatibilityMappings);
                    }
                }
            }

            private void InitNetStandard()
            {
                // populate the list of frameworks that could be compatible with NetStandard
                AddCompatibleCandidates();

                // populate the list of NetStandard versions
                AddNetStandardVersions();
            }

            private void AddShortNameRewriteMappings(IEnumerable<KeyValuePair<NuGetFramework, NuGetFramework>> mappings)
            {
                if (mappings != null)
                {
                    foreach (var mapping in mappings)
                    {
                        if (!_shortNameRewrites.ContainsKey(mapping.Key))
                        {
                            _shortNameRewrites.Add(mapping.Key, mapping.Value);
                        }
                    }
                }
            }

            private void AddFullNameRewriteMappings(IEnumerable<KeyValuePair<NuGetFramework, NuGetFramework>> mappings)
            {
                if (mappings != null)
                {
                    foreach (var mapping in mappings)
                    {
                        if (!_fullNameRewrites.ContainsKey(mapping.Key))
                        {
                            _fullNameRewrites.Add(mapping.Key, mapping.Value);
                        }
                    }
                }
            }

            private void AddCompatibilityMappings(IEnumerable<OneWayCompatibilityMappingEntry> mappings)
            {
                if (mappings != null)
                {
                    foreach (var mapping in mappings)
                    {
                        if (!_compatibilityMappings.TryGetValue(mapping.TargetFrameworkRange.Min.Framework, out HashSet<OneWayCompatibilityMappingEntry>? entries))
                        {
                            entries = new HashSet<OneWayCompatibilityMappingEntry>(OneWayCompatibilityMappingEntry.Comparer);
                            _compatibilityMappings.Add(mapping.TargetFrameworkRange.Min.Framework, entries);
                        }

                        entries.Add(mapping);
                    }
                }
            }

            private void AddSubSetFrameworks(IEnumerable<KeyValuePair<string, string>> mappings)
            {
                if (mappings != null)
                {
                    foreach (var mapping in mappings)
                    {
                        if (!_subSetFrameworks.TryGetValue(mapping.Value, out HashSet<string>? subSets))
                        {
                            subSets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            _subSetFrameworks.Add(mapping.Value, subSets);
                        }

                        subSets.Add(mapping.Key);
                    }
                }
            }

            /// <summary>
            /// 2 way per framework profile equivalence
            /// </summary>
            /// <param name="mappings"></param>
            private void AddEquivalentProfiles(IEnumerable<FrameworkSpecificMapping> mappings)
            {
                if (mappings != null)
                {
                    foreach (var profileMapping in mappings)
                    {
                        var frameworkIdentifier = profileMapping.FrameworkIdentifier;
                        var profile1 = profileMapping.Mapping.Key;
                        var profile2 = profileMapping.Mapping.Value;

                        if (!_equivalentProfiles.TryGetValue(frameworkIdentifier, out Dictionary<string, HashSet<string>>? profileMappings))
                        {
                            profileMappings = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                            _equivalentProfiles.Add(frameworkIdentifier, profileMappings);
                        }

                        if (!profileMappings.TryGetValue(profile1, out HashSet<string>? innerMappings1))
                        {
                            innerMappings1 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            profileMappings.Add(profile1, innerMappings1);
                        }

                        if (!profileMappings.TryGetValue(profile2, out HashSet<string>? innerMappings2))
                        {
                            innerMappings2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            profileMappings.Add(profile2, innerMappings2);
                        }

                        innerMappings1.Add(profile2);
                        innerMappings2.Add(profile1);
                    }
                }
            }

            /// <summary>
            /// 2 way framework equivalence
            /// </summary>
            /// <param name="mappings"></param>
            private void AddEquivalentFrameworks(IEnumerable<KeyValuePair<NuGetFramework, NuGetFramework>> mappings)
            {
                if (mappings != null)
                {
                    foreach (var pair in mappings)
                    {
                        var remaining = new Stack<NuGetFramework>();
                        remaining.Push(pair.Key);
                        remaining.Push(pair.Value);

                        var seen = new HashSet<NuGetFramework>();
                        while (remaining.Any())
                        {
                            var next = remaining.Pop();
                            if (!seen.Add(next))
                            {
                                continue;
                            }

                            if (!_equivalentFrameworks.TryGetValue(next, out HashSet<NuGetFramework>? eqFrameworks))
                            {
                                // initialize set
                                eqFrameworks = new HashSet<NuGetFramework>();
                                _equivalentFrameworks.Add(next, eqFrameworks);
                            }
                            else
                            {
                                // explore all equivalent
                                foreach (var framework in eqFrameworks)
                                {
                                    remaining.Push(framework);
                                }
                            }
                        }

                        // add this equivalency rule, enforcing transitivity
                        foreach (var framework in seen)
                        {
                            foreach (var other in seen)
                            {
                                if (!NuGetFramework.Comparer.Equals(framework, other))
                                {
                                    _equivalentFrameworks[framework].Add(other);
                                }
                            }
                        }

                    }
                }
            }

            private void AddFrameworkSynonyms(IEnumerable<KeyValuePair<string, string>> mappings)
            {
                if (mappings != null)
                {
                    foreach (var pair in mappings)
                    {
                        if (!_identifierSynonyms.ContainsKey(pair.Key))
                        {
                            _identifierSynonyms.Add(pair.Key, pair.Value);
                        }
                    }
                }
            }

            private void AddIdentifierShortNames(IEnumerable<KeyValuePair<string, string>> mappings)
            {
                if (mappings != null)
                {
                    foreach (var pair in mappings)
                    {
                        var shortName = pair.Value;
                        var longName = pair.Key;

                        if (!_identifierSynonyms.ContainsKey(pair.Value))
                        {
                            _identifierSynonyms.Add(pair.Value, pair.Key);
                        }

                        _identifierShortToLong.Add(shortName, longName);

                        _identifierToShortName.Add(longName, shortName);
                    }
                }
            }

            private void AddProfileShortNames(IEnumerable<FrameworkSpecificMapping> mappings)
            {
                if (mappings != null)
                {
                    foreach (var profileMapping in mappings)
                    {
                        _profilesToShortName.Add(profileMapping.Mapping.Value, profileMapping.Mapping.Key);
                        _profileShortToLong.Add(profileMapping.Mapping.Key, profileMapping.Mapping.Value);
                    }
                }
            }

            // Add supported frameworks for each portable profile number
            private void AddPortableProfileMappings(IEnumerable<KeyValuePair<int, NuGetFramework[]>> mappings)
            {
                if (mappings != null)
                {
                    foreach (var pair in mappings)
                    {
                        if (!_portableFrameworks.TryGetValue(pair.Key, out HashSet<NuGetFramework>? frameworks))
                        {
                            frameworks = new HashSet<NuGetFramework>();
                            _portableFrameworks.Add(pair.Key, frameworks);
                        }

                        foreach (var fw in pair.Value)
                        {
                            frameworks.Add(fw);
                        }
                    }
                }
            }

            // Add optional frameworks for each portable profile number
            private void AddPortableOptionalFrameworks(IEnumerable<KeyValuePair<int, NuGetFramework[]>> mappings)
            {
                if (mappings != null)
                {
                    foreach (var pair in mappings)
                    {
                        if (!_portableOptionalFrameworks.TryGetValue(pair.Key, out HashSet<NuGetFramework>? frameworks))
                        {
                            frameworks = new HashSet<NuGetFramework>();
                            _portableOptionalFrameworks.Add(pair.Key, frameworks);
                        }

                        foreach (var fw in pair.Value)
                        {
                            frameworks.Add(fw);
                        }
                    }
                }
            }

            private void AddPortableCompatibilityMappings(IEnumerable<KeyValuePair<int, FrameworkRange>> mappings)
            {
                if (mappings != null)
                {
                    foreach (var mapping in mappings)
                    {
                        if (!_portableCompatibilityMappings.TryGetValue(mapping.Key, out HashSet<FrameworkRange>? entries))
                        {
                            entries = new HashSet<FrameworkRange>(FrameworkRangeComparer.Instance);
                            _portableCompatibilityMappings.Add(mapping.Key, entries);
                        }

                        entries.Add(mapping.Value);
                    }
                }
            }

            // Ordered lists of framework identifiers
            public void AddFrameworkPrecedenceMappings(IDictionary<string, int> destination, IEnumerable<string> mappings)
            {
                if (mappings != null)
                {
                    foreach (var framework in mappings)
                    {
                        if (!destination.ContainsKey(framework))
                        {
                            destination.Add(framework, destination.Count);
                        }
                    }
                }
            }

            public bool TryGetCompatibilityMappings(NuGetFramework framework, [NotNullWhen(true)] out IEnumerable<FrameworkRange>? supportedFrameworkRanges)
            {
                if (_compatibilityMappings.TryGetValue(framework.Framework, out HashSet<OneWayCompatibilityMappingEntry>? entries))
                {
                    supportedFrameworkRanges = entries.Where(m => m.TargetFrameworkRange.Satisfies(framework)).Select(m => m.SupportedFrameworkRange);
                    return supportedFrameworkRanges.Any();
                }

                supportedFrameworkRanges = null;
                return false;
            }

            public bool TryGetSubSetFrameworks(string frameworkIdentifier, [NotNullWhen(true)] out IEnumerable<string>? subSetFrameworks)
            {
                if (_subSetFrameworks.TryGetValue(frameworkIdentifier, out HashSet<string>? values))
                {
                    subSetFrameworks = values;
                    return true;
                }

                subSetFrameworks = null;
                return false;
            }

            public int CompareFrameworks(NuGetFramework? x, NuGetFramework? y)
            {
                if (x is null && y is null) return 0;
                if (x is null) return -1;
                if (y is null) return 1;

                // For the purposes of this compare do not treat netcore50 as packages based
                var xPackagesBased = x.IsPackageBased && !NuGetFrameworkUtility.IsNetCore50AndUp(x);
                var yPackagesBased = y.IsPackageBased && !NuGetFrameworkUtility.IsNetCore50AndUp(y);

                if (xPackagesBased != yPackagesBased)
                {
                    // non-package based always come before package based
                    return xPackagesBased.CompareTo(yPackagesBased);
                }

                var precedence = xPackagesBased ? _packageBasedFrameworkPrecedence : _nonPackageBasedFrameworkPrecedence;

                return CompareUsingPrecedence(x, y, precedence);
            }

            public int CompareEquivalentFrameworks(NuGetFramework? x, NuGetFramework? y)
            {
                return CompareUsingPrecedence(x, y, _equivalentFrameworkPrecedence);
            }

            private static int CompareUsingPrecedence(NuGetFramework? x, NuGetFramework? y, Dictionary<string, int> precedence)
            {
                if (x is null && y is null) return 0;
                if (x is null) return -1;
                if (y is null) return 1;

                if (StringComparer.OrdinalIgnoreCase.Equals(x.Framework, y.Framework))
                {
                    return 0;
                }

                int xIndex;
                if (!precedence.TryGetValue(x.Framework, out xIndex))
                {
                    xIndex = int.MaxValue;
                }

                int yIndex;
                if (!precedence.TryGetValue(y.Framework, out yIndex))
                {
                    yIndex = int.MaxValue;
                }

                return xIndex.CompareTo(yIndex);
            }


            public NuGetFramework GetShortNameReplacement(NuGetFramework framework)
            {
                // Replace the framework name if a rewrite exists
                if (!_shortNameRewrites.TryGetValue(framework, out NuGetFramework? result))
                {
                    result = framework;
                }

                return result;
            }

            public NuGetFramework GetFullNameReplacement(NuGetFramework framework)
            {
                // Replace the framework name if a rewrite exists
                if (!_fullNameRewrites.TryGetValue(framework, out NuGetFramework? result))
                {
                    result = framework;
                }

                return result;
            }

            public IEnumerable<NuGetFramework> GetNetStandardVersions()
            {
                return _netStandardVersions.AsReadOnly();
            }

            public IEnumerable<NuGetFramework> GetCompatibleCandidates()
            {
                return _compatibleCandidates.AsReadOnly();
            }

            private void AddNetStandardVersions()
            {
                foreach (var framework in _compatibleCandidates)
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(framework.Framework, FrameworkConstants.FrameworkIdentifiers.NetStandard))
                    {
                        _netStandardVersions.Add(framework);
                    }
                }

                _netStandardVersions.Sort(NuGetFrameworkSorter.Instance);
            }

            private void AddCompatibleCandidates()
            {
                var set = new HashSet<NuGetFramework>();

                // equivalent
                foreach (var framework in _equivalentFrameworks.Values.SelectMany(x => x))
                {
                    set.Add(framework);
                }

                // compatible
                foreach (var mapping in _compatibilityMappings.SelectMany(p => p.Value))
                {
                    set.Add(mapping.TargetFrameworkRange.Min);
                    set.Add(mapping.TargetFrameworkRange.Max);
                    set.Add(mapping.SupportedFrameworkRange.Min);
                    set.Add(mapping.SupportedFrameworkRange.Max);
                }

                // portable compatible
                foreach (var pair in _portableCompatibilityMappings)
                {
                    var portable = new NuGetFramework(
                        FrameworkConstants.FrameworkIdentifiers.Portable,
                        FrameworkConstants.EmptyVersion,
                        string.Format(NumberFormatInfo.InvariantInfo, "Profile{0}", pair.Key));

                    set.Add(portable);
                    foreach (var range in pair.Value)
                    {
                        set.Add(range.Min);
                        set.Add(range.Max);
                    }
                }

                // subset and superset
                var superSetFrameworks = _subSetFrameworks
                    .SelectMany(p => p.Value.Select(subset => new { Superset = p.Key, Subset = subset }))
                    .GroupBy(p => p.Subset, p => p.Superset, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => new HashSet<string>(g, StringComparer.OrdinalIgnoreCase));

                foreach (var framework in set.ToArray())
                {
                    if (framework.HasProfile)
                    {
                        continue;
                    }

                    if (_subSetFrameworks.TryGetValue(framework.Framework, out HashSet<string>? subset))
                    {
                        foreach (var subFramework in subset)
                        {
                            set.Add(new NuGetFramework(subFramework, framework.Version, framework.Profile));
                        }
                    }

                    if (superSetFrameworks.TryGetValue(framework.Framework, out HashSet<string>? superset))
                    {
                        foreach (var superFramework in superset)
                        {
                            set.Add(new NuGetFramework(superFramework, framework.Version, framework.Profile));
                        }
                    }
                }

                _compatibleCandidates.AddRange(set);
                _compatibleCandidates.Sort(NuGetFrameworkSorter.Instance);
            }

            // Strong typed non-IEnumerator based HashSet functions
            private static bool SetEquals(HashSet<NuGetFramework> left, HashSet<NuGetFramework> right)
            {
                if (left.Count != right.Count)
                {
                    return false;
                }

                foreach (var fw in left)
                {
                    if (!right.Contains(fw))
                    {
                        return false;
                    }
                }

                return true;
            }

            private static void UnionWith(HashSet<NuGetFramework> toAccumulate, HashSet<NuGetFramework> toAdd)
            {
                foreach (var fw in toAdd)
                {
                    toAccumulate.Add(fw);
                }
            }
        }

        internal sealed class DefaultFrameworkNameProvider : FrameworkNameProvider
        {
            public DefaultFrameworkNameProvider()
                : base(new IFrameworkMappings[] { DefaultFrameworkMappings.Instance },
                    new IPortableFrameworkMappings[] { DefaultPortableFrameworkMappings.Instance })
            {
            }

            private static readonly Lazy<IFrameworkNameProvider> InstanceLazy = new Lazy<IFrameworkNameProvider>(() => new DefaultFrameworkNameProvider());

            public static IFrameworkNameProvider Instance
            {
                get { return InstanceLazy.Value; }
            }
        }
    }
}
