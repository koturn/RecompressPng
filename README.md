RecompressPng
=============

[![.NET](https://github.com/koturn/RecompressPng/actions/workflows/dotnet.yml/badge.svg?branch=main)](https://github.com/koturn/RecompressPng/actions/workflows/dotnet.yml)

[![Test status](https://ci.appveyor.com/api/projects/status/pic7w57ggpfcs7qx/branch/main?svg=true)](https://ci.appveyor.com/project/koturn/recompresspng "AppVeyor | koturn/RecompressPng")

PNG re-compressing tool with [Zopfli Compression Algorithm](https://github.com/google/zopfli "google/zopfli").

- Parallel executable
- Can process PNG files in zip files
- Don't rewrite the timestamp


## Usage

```shell
> RecompressPng.exe [Zip Archive or Directory]
```


## Build

First, pull all submodules.

```shell
> git submodule update --init --recursive
```

Second, build whole project.

```shell
> nmake
```


## Depedent Libraries

The following libraries are managed as submodules.

- [koturn/Koturn.CommandLine](https://github.com/koturn/Koturn.CommandLine "koturn/Koturn.CommandLine")
- [koturn/Koturn.NativeCode](https://github.com/koturn/Koturn.NativeCode "koturn/Koturn.NativeCode")
- [koturn/Koturn.Zopfli](https://github.com/koturn/Koturn.Zopfli "koturn/Koturn.Zopfli")
    - [google/zopfli](https://github.com/google/zopfli "google/zopfli")


## LICENSE

This software is released under the MIT License, see [LICENSE](LICENSE "LICENSE").
