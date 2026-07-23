# Background

SQL Server isn't a black box where what happens inside is a mystery. There's a huge amount of information on what it is doing but to understand it requires knowledge about the internal architecture. What is a clustered index? What is a heap? What is the difference between a DATETIME and SMALLDATETIME? What is the effect on my table structure if a field is NULL vs NOT NULL, or CHAR vs VARCHAR?

To get answers to these questions and to learn about internals if you want to see it and experiment you have to start digging around using system views and undocumented commands.

My first introduction to internals was a training course I attended by [Kimberly Tripp](https://www.sqlskills.com/about/kimberly-l-tripp/). The company I was working for at the time was in a programme for the pre-release version of SQL Server 2005 and it was fascinating that you could actually run commands and see how things actually worked.

Another interesting part of looking into internals is that it is like an archaeological dig. Fundamentally a lot of the internals structures have been the same, for example the 8KB page with 96 byte header has been around since SQL Server 7 released in 1998. A lot of new features still fit into this design. Even for columnstores it uses 8KB Blob pages. Try `sp_helptext` on sys views and you may see very old objects at the [core of the database](https://learn.microsoft.com/en-us/sql/relational-databases/system-tables/system-base-tables). This is actually useful to understand new functionality as it gives context to how and why the new features are implemented.

## Internals Viewer

When I started looking into internals there were some great resources, including Kalen Delaney's book [Inside Microsoft® SQL Server™ 2005: The Storage Engine](https://www.oreilly.com/library/view/inside-microsoft-r-sql/9780735621053/) and [Paul S. Randal's detailed blog posts](https://www.sqlskills.com/blogs/paul/).

I ended up with screens full of queries and page dumps which were difficult to keep track of, so I decided to see what I could do to create a visual tool to help in navigating around all this information. Maybe the subconscious inspiration was from the mild excitement of running a disk defrag in Windows 95 and seeing files move around. From that Internals Viewer evolved, displaying first the allocations, then pages, then per-row interpretations, and eventually the new query tracing functionality.

It is a free tool and not intended to be commercial. Personally I've learnt a huge amount from creating it. Most of it was created in the days before LLMs and you can look into the application code for further insight into how structures are decoded.
