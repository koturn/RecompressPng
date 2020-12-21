RecompressPng
=============

[![Test status](https://ci.appveyor.com/api/projects/status/pic7w57ggpfcs7qx/branch/dev-depend-zopfli?svg=true)](https://ci.appveyor.com/project/koturn/recompresspng "AppVeyor | koturn/RecompressPng")

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

Third, place zopfli.dll and zopflipng.dll in an appropriate location.

zopfli.dll and zopflipng.dll can be built by following the steps below.

```shell
> git clone https://github.com/google/zopfli
> cd zopfli
> mkdir build
> cd build
> cmake .. -G "NMake Makefile" -DCMAKE_BUILD_TYPE=Release -DZOPFLI_BUILD_SHARED=ON
> cmake --build .
```


## Depedent Libraries

- [zopfli](https://github.com/google/zopfli "google/zopfli")


## LICENSE

This software is released under the MIT License, see [LICENSE](LICENSE "LICENSE").
