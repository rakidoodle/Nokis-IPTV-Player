# Runtime dependencies

Versions are centralized in `Directory.Packages.props`. Preview packages are not permitted.

| Package | Version | Purpose | License |
| --- | ---: | --- | --- |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM observable properties and commands | MIT |
| Microsoft.Extensions.Hosting | 10.0.11 | Application lifetime, configuration, and dependency injection | MIT |
| Microsoft.Extensions.Http | 10.0.11 | Managed `HttpClient` creation | MIT |
| Microsoft.Extensions.Logging.Debug | 10.0.11 | Structured development logging | MIT |
| Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.11 | Validated configuration binding | MIT |
| Microsoft.Data.Sqlite | 10.0.11 | Local SQLite database access | MIT |
| System.Security.Cryptography.ProtectedData | 10.0.11 | Windows DPAPI credential protection | MIT |
| LibVLCSharp / LibVLCSharp.WPF | 3.10.1 | Managed media engine and WPF video surface | LGPL-2.1-or-later |
| VideoLAN.LibVLC.Windows | 3.0.23.1 | Native Windows playback runtime, codecs, and demuxers | LGPL-2.1-or-later |
| MSTest | 4.3.3 | Automated testing | MIT |

The packages are maintained by Microsoft, the .NET ecosystem, or VideoLAN and are suitable for normal application distribution subject to their listed licenses. Transitive dependencies are reviewed through NuGet restore and vulnerability auditing.
