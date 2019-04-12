### Paket.CredentialProvider.Gen2Support

## The Problem

There are 2 different NuGet credential providers; Gen 1, and Gen2.

### [Gen 1](https://docs.microsoft.com/en-us/nuget/reference/extensibility/nuget-exe-credential-providers)

This was the first effort by the NuGet team to introduce credential providers. The interface is simple, they print some JSON to stdout with a status code. The NuGet team overlooked .NET Core support, because the discovery process assumed they would be named `CredentialProvider.*.exe`.

Paket added Gen 1 provider support, and in addition supported .NET Core by changing the discovery process to include `CredentialProvider.*.dll`, running those under .NET Core. With that said, there are no credential providers I am aware of that adopt this mechanism.

### [Gen 2](https://docs.microsoft.com/en-us/nuget/reference/extensibility/nuget-cross-platform-authentication-plugin)

The NuGet team realised they needed .NET Core support, and cross-plat support. At the same time, they wanted to tackle [NuGets plugin architecture](https://docs.microsoft.com/en-us/nuget/reference/extensibility/nuget-cross-platform-plugins), plugin trust and more. The result was a more complex system, where there is a JSON protocol over stdin/stdout with handshakes and all sorts.

Paket has yet to adopt this new mechanism.

| Provider                       | Gen    | Windows Support | macOS Support | ADO Distributed | NuGet Client Support | Paket Support |
|--------------------------------|--------|-----------------|---------------|-----------------|----------------------|---------------|
| CredentialProvider.VSS         | Gen 1  | :heavy_check_mark: |               | :heavy_check_mark: | Going soon           | :heavy_check_mark: |
| CredentialProvider.Microsoft   | Gen 2  | :heavy_check_mark: | :heavy_check_mark: |                 | :heavy_check_mark: |               |
| CredentialProvider.Gen2Support | Gen 1  | :heavy_check_mark: | :heavy_check_mark: |                 | Going soon           | :heavy_check_mark: |

## This Shim

This package acts as a shim by implementing a Gen 1 provider, that talks to Gen 2 providers. It has knowledge of the Azure Artifacts Credential Provider so that under non-Windows it can ask you to run the appropriate command to authenticate.

This shim solves the problem of adopting Gen 2 pacakges with Paket while the eco-systems align.

## Installation

### macOS

Create a folder named CredentialProviders under:
`$HOME/.local/share/NuGet`

Download the latest `CredentialProvider.Gen2Support.zip` from the releases tab and unzip the contents of into it.

Be sure to have installed the Azure Artifacts Credential Provider, under `$HOME/.nuget/plugins`.
