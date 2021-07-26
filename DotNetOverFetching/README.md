# Dotnet Overfetching

One of my common code review comments is about over-fetching from the database;
e.g. running a `SELECT *` instead of `SELECT Name, Email`.

This experiment is intended to test if that matters anymore.

## Experiment design

We have a few variables of interest

- ORM library: Dapper, EFCore
- table: narrow, wide, has blob
- over-fetching: yes, no

We'll test all combinations and see if we can draw any conclusions.

Test harness

- using mssql running in docker
- EFCore code-first to generate the database
- benchmarkdotnet

Confounding factors:

- all connections are on localhost; effects of network latency will not be
  covered. A test against a cloud DB would be easy, but going between my laptop
  and the cloud would be unrealistic. Follow-up work would be to run this test
  in the cloud in the same DC as the db
- specific versions of libraries

Hypothesis:

overfetching is slower, but doesn't matter for small tables.

Notes

https://docs.microsoft.com/en-us/sql/relational-databases/tables/tables?view=sql-server-2017
says 1024 is the limit, test table widths up to 512

Comparing EF to Dapper is not relevant

Code-generator for the big table classes:

```js
const types = [
  "int",
  "bool",
  "Guid",
  "DateTimeOffset",
  "string",
  "int?",
  "Guid?",
  "DateTimeOffset?",
];
Array(8)
  .fill(0)
  .map((x, i) => types[i % types.length])
  .map((x, i) => `public ${x} Col${i} {get; set;}`)
  .join("\n");
```

Procedure

```console
# start up the db
$ docker-compose up --detach
# add some test data
$ dotnet run -c Release -- seed
# run EfCore test
$ dotnet run -c Release -- --filter "*EfCore*"
# run Dapper test
$ dotnet run -c Release -- --filter "*Dapper*"
$ R BATCH -f BuildPlots.R
```
