# Internals Viewer 2024

>This is based on the codebase for Internals Viewer from 2007. The code has been upgraded to .NET Core 8 and I've started modernizing things. I'll hopefully be able to refactor and add testing to bring the 16 year old code back to life and up to scratch.

## Introduction

Internals Viewer is a visualisation tool for viewing the internals of the SQL Server Storage Engine.

### Background

In 2006 I started looking into SQL Server internals to get a better understanding of what was going on inside of a database. SQL Server isn't a black box where what happens inside is a mystery. There's a huge amount of information on what it is doing but to understand it requires knowledge about the internal architecture. What is a Clustered Index? What is a Heap? What is the difference between a DATETIME and SMALLDATETIME? What is the effect on my table structure if a field is NULL vs NOT NULL, or CHAR vs VARCHAR?

To get answers to these questions and to learn about internals if you want to see it and experiment you have to start digging around using system tables and undocumented commands. There is a lot of information out there in articles, blogs, and books. I've listed some below. 

When I started doing this I found a couple of things. The first thing was it's not complicated! I was suprised at how accesible all of the information was. The second thing I found was the techniques to view internals were cumbersome. You have to query sys tables, take values and convert them from binary, run a DBCC command, view the results, follow to another page, run another command etc.

Internals Viewer started to make it easier to navigate around the internals of a database and view the data structures. Over time it has implemented more of the interpretation to help with explanation of structures in the user interface.

## Concepts

# Pages

Data[^1] is managed inside SQL Server with 8KB chunks of data called Pages. All pages have a 96 byte header that give detail about the page, including things like the page type and links to other pages.

Pages types include allocation structures, index data, table data etc.

[^1]: This applies to the MDF/NDF database files. Functionality such as the In-Memory OLTP use different data structures

## Page Address

Pages are identified with a page address in the format File Id:Page Id. This is where the page is located in the database (MDF/NDF) files.

# Allocations

The first thing you see when you open Internals Viewer is an allocation map. This is a visualisation of the internal structures SQL Server uses to track the physical location of objects.

Each block represents a page. Pages are tracked in groups of 8 pages called extents. An extent covers 64 KB in the file.

## Resources

### Websites
- [Paul S. Randal - In Recovery... Blog](https://www.sqlskills.com/blogs/paul/category/inside-the-storage-engine/)
- [Microsoft Learn - Page and Extents Architecture Guide](https://learn.microsoft.com/en-us/sql/relational-databases/pages-and-extents-architecture-guide)

### Books

- [Microsoft SQL Server 2012 Internals (Developer Reference) by Kalen Delaney and Craig Freeman](https://www.amazon.co.uk/Microsoft-SQL-Server-2012-Internals-ebook/dp/B00JDMQJYC)

## Future Development
I've added a test winforms app to get it running. Once I've done a bit more refactoring I'll look into adding it in as a SSMS extension again.

After that I'll look into if any new features since SQL Server 2008 can be added, for example if Column Store indexes.
