NEventStore.Persistence.Sql
===

SQL Persistence Engine for NEventStore

NEventStore.Persistence.Sql currently supports:

- .net framework 4.6.2
- .net standard 2.0
- MsSql
- SqlLite
- MySql
- PostgreSQL
- Oracle (not tested)

Build Status
===

Branches:

- master [![Build status](https://ci.appveyor.com/api/projects/status/5difan7hap8vwhwe/branch/master?svg=true)](https://ci.appveyor.com/project/AGiorgetti/neventstore-persistence-sql/branch/master)
- develop [![Build status](https://ci.appveyor.com/api/projects/status/5difan7hap8vwhwe/branch/develop?svg=true)](https://ci.appveyor.com/project/AGiorgetti/neventstore-persistence-sql/branch/develop)

## PostgreSQL Warning

If you upgrade Npgsql to version 6.0 and up you must take into account the breaking changes
made about the timezones handling [Timestamp rationalization and improvements](https://www.npgsql.org/efcore/release-notes/6.0.html#timestamp-rationalization-and-improvements).

Possible solutions:
- manually migrate the Table schema and update the "CommitStamp" column type from "timestamp" to "timestamptz".
- disable the new behavior by calling:
  ```
  AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
  ```
- use the new `PostgreNpgsql6Dialect`.

## How to Build (locally)

- Clone the repository with:

```
git clone --recursive https://github.com/NEventStore/NEventStore.Persistence.SQL.git
```

or

```
git clone https://github.com/NEventStore/NEventStore.Persistence.SQL.git
git submodule update
```

To build the project locally on a Windows Machine:

- Optional: update `.\src\.nuget\NEventStore.Persistence.Sql.nuspec` file if needed (before creating relase packages).
- Open a Powershell console in Administrative mode and run the build script `build.ps1` in the root of the repository.

## How to Run Unit Tests (locally)

- Install Database engines or use Docker to run them in a container (you can use the scripts in `./docker` folder).
- Define the following environment variables:

  ```
  NEventStore.MsSql="Server=localhost,50001;Database=NEventStore;User Id=sa;Password=Password1;TrustServerCertificate=True;"
  NEventStore.MySql="Server=localhost;Port=50003;Database=NEventStore;Uid=sa;Pwd=Password1;AutoEnlist=false;"
  NEventStore.PostgreSql="Server=localhost;Port=50004;Database=NEventStore;Uid=sa;Pwd=Password1;Enlist=false;"
  NEventStore.Oracle="Data Source=localhost:1521/XE;User Id=system;Password=Password1;Persist Security Info=True;"
  ```

## How to contribute

### Git-Flow

This repository uses GitFlow to develop, if you are not familiar with GitFlow you can look at the following link.

* [A Successful Git Branching Model](http://nvie.com/posts/a-successful-git-branching-model/)
* [Git Flow Cheat-Sheet](http://danielkummer.github.io/git-flow-cheatsheet/)
* [Git Flow for GitHub](https://datasift.github.io/gitflow/GitFlowForGitHub.html)

### Installing and configuring Git Flow

Probably the most straightforward way to install GitFlow on your machine is installing [Git Command Line](https://git-for-windows.github.io/), then install the [Visual Studio Plugin for Git-Flow](https://visualstudiogallery.msdn.microsoft.com/27f6d087-9b6f-46b0-b236-d72907b54683). This plugin is accessible from the **Team Explorer** menu and allows you to install GitFlow extension directly from Visual Studio with a simple click. The installer installs standard GitFlow extension both for command line and for Visual Studio Plugin.

Once installed you can use GitFlow right from Visual Studio or from Command line, which one you prefer.

### Build machine and GitVersion

Build machine uses [GitVersion](https://github.com/GitTools/GitVersion) to manage automatic versioning of assemblies and Nuget Packages. You need to be aware that there are a rule that does not allow you to directly commit on master, or the build will fail. 

A commit on master can be done only following the [Git-Flow](http://nvie.com/posts/a-successful-git-branching-model/) model, as a result of a new release coming from develop, or with an hotfix. 

### Quick Info for NEventstore projects

Just clone the repository and from command line checkout develop branch with 

```
git checkout develop
```

Then from command line run GitFlow initialization scripts

```
git flow init
```

You can leave all values as default. Now your repository is GitFlow enabled.

### Note on Nuget version on Nuspec

Remember to update `.\src\.nuget\NEventStore.Persistence.Sql.nuspec` file if needed (before creating relase packages).

The .nuspec file is needed because the new `dotnet pack` command has problems dealing with ProjectReferences, submodules get the wrong version number.

While we are on develop branch, (suppose we just bumped major number so the driver version number is 6.0.0-unstablexxxx), we need to declare that this persistence driver depends from a version greater than the latest published. If the latest version of NEventStore 5.x.x wave iw 5.4.0 we need to declare this package dependency as

(5.4, 7)

This means, that we need a NEventStore greater than the latest published, but lesser than the next main version. This allows version 6.0.0-unstable of NEventStore to satisfy the dependency. We remember that prerelease package are considered minor than the stable package. Es.

5.4.0
5.4.1
6.0.0-unstable00001
6.0.0