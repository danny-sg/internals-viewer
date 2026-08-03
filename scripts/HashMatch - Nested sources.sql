/* -----------------------------------------------------------------------------------------------------------------------
    Internals Viewer
 -----------------------------------------------------------------------------------------------------------------------
    Hash Match - nested sources

    Queries where a hash match reads another hash match rather than a table, for testing operator chaining.

    FORCE ORDER fixes the join order so the shape stays put between runs, and MAXDOP 1 keeps it row mode with no
    exchanges in the tree.

    Run against AdventureWorks.
 ----------------------------------------------------------------------------------------------------------------------- */

USE AdventureWorks2022;
GO

/* -----------------------------------------------------------------------------------------------------------------------
    1. Three tables, small - the easiest shape to follow by hand

    Category has 4 rows and Subcategory 37, so the first hash table is tiny and its results are obvious.
 ----------------------------------------------------------------------------------------------------------------------- */
SELECT   pc.Name AS Category
        ,ps.Name AS Subcategory
        ,p.Name  AS Product
FROM     Production.ProductCategory pc
         INNER HASH JOIN Production.ProductSubcategory ps
           ON ps.ProductCategoryID = pc.ProductCategoryID
         INNER HASH JOIN Production.Product p
           ON p.ProductSubcategoryID = ps.ProductSubcategoryID
OPTION (MAXDOP 1, FORCE ORDER);
GO

/* -----------------------------------------------------------------------------------------------------------------------
    2. The nested join on the PROBE side

    The first join builds from Category, and the whole of that join becomes the probe input of the second.
 ----------------------------------------------------------------------------------------------------------------------- */
SELECT   p.Name AS Product
        ,ps.Name AS Subcategory
FROM     Production.Product p
         INNER HASH JOIN (
             SELECT ps2.ProductSubcategoryID
                   ,ps2.Name
             FROM   Production.ProductCategory pc2
                    INNER HASH JOIN Production.ProductSubcategory ps2
                      ON ps2.ProductCategoryID = pc2.ProductCategoryID
         ) ps
           ON ps.ProductSubcategoryID = p.ProductSubcategoryID
WHERE    p.ProductID < 800
OPTION (MAXDOP 1, FORCE ORDER);
GO

/* -----------------------------------------------------------------------------------------------------------------------
    3. Three levels deep

    Two nested joins, so the outermost reads a join that itself reads a join.
 ----------------------------------------------------------------------------------------------------------------------- */
SELECT   pc.Name AS Category
        ,ps.Name AS Subcategory
        ,p.Name  AS Product
        ,pm.Name AS Model
FROM     Production.ProductCategory pc
         INNER HASH JOIN Production.ProductSubcategory ps
           ON ps.ProductCategoryID = pc.ProductCategoryID
         INNER HASH JOIN Production.Product p
           ON p.ProductSubcategoryID = ps.ProductSubcategoryID
         INNER HASH JOIN Production.ProductModel pm
           ON pm.ProductModelID = p.ProductModelID
OPTION (MAXDOP 1, FORCE ORDER);
GO

/* -----------------------------------------------------------------------------------------------------------------------
    4. Larger, to see a nested join's results feed a real build

    SalesOrderHeader is bounded so the first hash table stays a sensible size to step through.
 ----------------------------------------------------------------------------------------------------------------------- */
SELECT   soh.SalesOrderID
        ,sod.OrderQty
        ,p.Name AS Product
FROM     Sales.SalesOrderHeader soh
         INNER HASH JOIN Sales.SalesOrderDetail sod
           ON sod.SalesOrderID = soh.SalesOrderID
         INNER HASH JOIN Production.Product p
           ON p.ProductID = sod.ProductID
WHERE    soh.SalesOrderID BETWEEN 43659 AND 43700
OPTION (MAXDOP 1, FORCE ORDER);
GO

/* -----------------------------------------------------------------------------------------------------------------------
    5. Mixed operators above a nested join

    A TOP over the outermost join, for checking that a nested tree still stops early.
 ----------------------------------------------------------------------------------------------------------------------- */
SELECT   TOP (25)
         pc.Name AS Category
        ,ps.Name AS Subcategory
        ,p.Name  AS Product
FROM     Production.ProductCategory pc
         INNER HASH JOIN Production.ProductSubcategory ps
           ON ps.ProductCategoryID = pc.ProductCategoryID
         INNER HASH JOIN Production.Product p
           ON p.ProductSubcategoryID = ps.ProductSubcategoryID
OPTION (MAXDOP 1, FORCE ORDER);
GO
