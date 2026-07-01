/*
-----------------------------------------------------------------------------------------------------------------------
 Internals Viewer - Sample Queries
-----------------------------------------------------------------------------------------------------------------------

 Sample queries reference the AdventureWorks sample database:

  https://learn.microsoft.com/en-us/sql/samples/adventureworks-install-configure

*/

-- Clustered Index Scan
SELECT *
FROM   Sales.SalesOrderDetail

-- Clustered Index Scan (Predicate)
SELECT *
FROM   Sales.SalesOrderDetail
WHERE  ProductID = 800

-- Clustered Index Seek
SELECT *
FROM   Sales.SalesOrderDetail
WHERE  SalesOrderID = 50000

-- Clustered Index Seek (Range/Seek Predicate)
SELECT *
FROM   Sales.SalesOrderDetail
WHERE  SalesOrderID BETWEEN 70000 AND 80000