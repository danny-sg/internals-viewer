# Resources

## Books

[Microsoft SQL Server 2012 Internals by Kalen Delaney et al](https://www.oreilly.com/library/view/microsoft-sql-server/9780735670174/)

- Previous editions
  - [Microsoft® SQL Server® 2008 Internals](https://www.oreilly.com/library/view/microsoft-r-sql-server-r/9780735634787/)

  - [The Storage Engine: Inside Microsoft® SQL Server 2005](https://www.amazon.co.uk/Inside-Microsoft%C2%AE-SQL-ServerTM-2005/dp/0735621055)

[Pro SQL Server Internals by Dmitri Korotkevitch](https://www.oreilly.com/library/view/pro-sql-server/9781484219645/)

## Blogs

[Paul S. Randal - In Recovery](https://www.sqlskills.com/blogs/paul/)

- The "Inside the Storage Engine" posts were the starting point of Internals Viewer

[Paul White - sql.kiwi](https://www.sql.kiwi/)

- Detailed optimizer and query processor internals information

## Websites

[SQLServerFast - Execution Plan Reference](https://sqlserverfast.com/epr/)

- Detailed information about execution plan operators, including algorithms

## Documentation

[Microsoft Learn - SQL - Internals & architecture](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-guides)

- [Pages and Extents](https://learn.microsoft.com/en-us/sql/relational-databases/pages-and-extents-architecture-guide)

## Internals Viewer

Internals Viewer itself was used to decipher undocumented functionality. For example, at the time PAGE and ROW level compression was not detailed anywhere, so adding it involved reverse engineering with `DBCC PAGE` and the decode selection functionality in the Page Viewer, translating bytes into possible values.

The repo includes a verification tool that compares Internals Viewer vs DBCC PAGE to verify the row cracking.
