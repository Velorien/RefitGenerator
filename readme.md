# Refit Generator
_aka ReGen_

[![nuget](https://img.shields.io/nuget/v/RefitGenerator)](https://www.nuget.org/packages/RefitGenerator/)

RefitGenerator is a global `dotnet tool` used to generate [Refit](https://github.com/reactiveui/refit) client code from OpenApi schemas.

## Usage

Typing `regen -h` will display a list of parameters:

* `-u` or `--url` - url to OpenApi json or yaml
* `-f` or `--file` - path to OpenApi json or yaml local file
* `-o` or `--outputDirectory` - where to put the generated files
  * defaults to the current directory
* `-p` or `--projectName` - project name and root namespace
  * defaults to the output directory name
* `--groupBy` or `--groupingStrategy` - method of grouping paths into interfaces
  * defaults to `FirstTag`
  * possible values
    * `FirstTag` - uses the first tag in the array for the given path
    * `MostCommonTag` - uses the most used tag, produces the smallest number of interfaces
    * `LeastCommonTag` - uses the least used tag, produces the largest number of finely grained interfaces
* `-r` or `--removeIfExists` - a flag which controls whether to delete the output directory if exists first
* `--executable` - generate a .NET 5 console app with a basic setup instead of .NET Standard 2.0 class library
* `--ignoreAllHeaders` - do not include any header parameters in the resulting code
* `--ignoredHeaders` - provide a list of headers to ignore, redundant if `--ignoreAllHeaders` flag is used
* `--addEqualsNullToOptionalParameters` - if a method parameter is optional, it is generated with a default value of null
* `--conflictingNameAffix` - adds an affix to a property if its name conflicts with the enclosing type name, not validated whether the resulting property name is a valid identifier
  * defaults to `Prop`
* `--prefixConflictingName` - if this flag is set, the `--conflictingNameAffix` will be a prefix, otherwise it will be a suffix