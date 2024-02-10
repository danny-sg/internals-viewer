# Background

In 2006 I started looking into SQL Server internals to get a better understanding of what was going on inside of a database. SQL Server isn't a black box where what happens inside is a mystery. There's a huge amount of information on what it is doing but to understand it requires knowledge about the internal architecture. What is a clustered index? What is a heap? What is the difference between a DATETIME and SMALLDATETIME? What is the effect on my table structure if a field is NULL vs NOT NULL, or CHAR vs VARCHAR?

To get answers to these questions and to learn about internals if you want to see it and experiment you have to start digging around using system views and undocumented commands. 

