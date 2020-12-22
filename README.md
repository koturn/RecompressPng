RecompressPng
=============

[![Test status](https://ci.appveyor.com/api/projects/status/pic7w57ggpfcs7qx/branch/dev-net5?svg=true)](https://ci.appveyor.com/project/koturn/recompresspng "AppVeyor | koturn/RecompressPng")

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
> git submodule update --init
```

Second, build whole project.

```shell
> msbuild /nologo /m /t:restore /p:Configuration=Release;Platform="Any CPU" RecompressPng.sln
> msbuild /nologo /m /p:Configuration=Release;Platform="Any CPU" RecompressPng.sln
```

If you use x86 environment, please run the following command instead.

```shell
> msbuild /nologo /m /t:restore /p:Configuration=Release;Platform="x86" RecompressPng.sln
> msbuild /nologo /m /p:Configuration=Release;Platform="x86" RecompressPng.sln
```


## Depedent Libraries

The following libraries are managed as submodules.

- [google/zopfli](https://github.com/google/zopfli "google/zopfli")
- [koturn/ArgumentParserSharp](https://github.com/koturn/ArgumentParserSharp "koturn/ArgumentParserSharp")
- [koturn/NativeCodeSharp](https://github.com/koturn/NativeCodeSharp "koturn/NativeCodeSharp")
- [koturn/ZopfliSharp](https://github.com/koturn/NativeCodeSharp "koturn/ZopfliSharp")


## LICENSE

This software is released under the MIT License, see [LICENSE](LICENSE "LICENSE").
