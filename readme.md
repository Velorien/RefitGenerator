# RefitGenerator
_aka `regen`_

[![nuget](https://img.shields.io/nuget/v/RefitGenerator)](https://www.nuget.org/packages/RefitGenerator/)

RefitGenerator is a global `dotnet tool` used to generate [Refit](https://github.com/reactiveui/refit) client code from OpenApi schemas.

## Installation

With .NET 5 installed run: `dotnet tool install -g refitgenerator`

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
* `--skipDeprecatedProperties` - if this flag is set, schema properties marked as `Deprecated` are not included in the model

## Caveats

* This tool does not resolve conflicting type names. For instance, if your schema defines a type which generates a class named `Environment`, you see an error that `System.Environment` conflicts with `YourNamespace.Models.Environment`
* If an object schema has nested, non-reference object schemas, the tool cannot give the nested nice names, so the resulting type name will be `ParentType_PropertyName`

## Dependencies

* [System.Commandline](https://github.com/dotnet/command-line-api) for parsing commandline parameters
* [OpenAPI.NET](https://github.com/microsoft/OpenAPI.NET) - for reading the OpenApi schemas

## Try it out

* [Github API](https://raw.githubusercontent.com/github/rest-api-description/main/descriptions/api.github.com/api.github.com.yaml) - causes the conflict described in Caveats
* [Petstore](https://petstore.swagger.io/v2/swagger.json) - the one and only
* [Slack API](https://raw.githubusercontent.com/slackapi/slack-api-specs/master/web-api/slack_web_openapi_v2_without_examples.json)
* [Official examples from spec](https://github.com/OAI/OpenAPI-Specification/tree/master/examples)