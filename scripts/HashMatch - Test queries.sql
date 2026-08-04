/*
-------------------------------------------------------------------------------------------------------------------
Hash Match Test Queries - AdventureWorks
-------------------------------------------------------------------------------------------------------------------

Every query is written so the Hash Match reads both of its inputs directly, which is what the trace needs.

    OPTION (MAXDOP 1)   A parallel hash join puts Repartition Streams between the join and the scans, so the inputs
                        are no longer direct reads and the Trace option will not appear.

    OPTION (HASH JOIN)  Forces the physical operator where the optimiser would otherwise choose loops or merge.

Anything that places a Sort, Aggregate, Filter or another join directly beneath the Hash Match has the same effect
as parallelism - the sides can no longer be reproduced by walking an index, so the join is not resolved.
-------------------------------------------------------------------------------------------------------------------
*/

USE AdventureWorks2022
GO

-- ------------------------------------------------------------------------------------------------------------------
-- 1. The plain case - small build, large probe, unique build keys
--
-- Product is the smaller side so it builds. Every build key is unique, so chains stay short and every comparison
-- that reaches a key test is a real match.
-- ------------------------------------------------------------------------------------------------------------------
SELECT      p.Name
           ,sod.OrderQty
FROM        Production.Product AS p
            INNER JOIN Sales.SalesOrderDetail AS sod
                ON sod.ProductID = p.ProductID
OPTION      (MAXDOP 1, HASH JOIN)
GO

-- ------------------------------------------------------------------------------------------------------------------
-- 2. Duplicate build keys - real collision chains
--
-- ProductInventory holds a row per product per location, so ProductID repeats on the build side. Chains grow and the
-- Chain badge appears on entries past the first.
-- ------------------------------------------------------------------------------------------------------------------
SELECT      pi.ProductID
           ,pi.Quantity
           ,sod.OrderQty
FROM        Production.ProductInventory AS pi
            INNER JOIN Sales.SalesOrderDetail AS sod
                ON sod.ProductID = pi.ProductID
OPTION      (MAXDOP 1, HASH JOIN)
GO

-- ------------------------------------------------------------------------------------------------------------------
-- 3. Probe residual - a non equi conjunct
--
-- SpecialOfferID hashes. OrderQty >= MinQty cannot be hashed, so it becomes the Probe Residual and is tested on each
-- pair that already matched on the key. Expect Hash and Key ticks with a Residual cross on the discarded pairs.
-- ------------------------------------------------------------------------------------------------------------------
SELECT      so.Description
           ,so.DiscountPct
           ,sod.OrderQty
FROM        Sales.SpecialOffer AS so
            INNER JOIN Sales.SalesOrderDetail AS sod
                ON  sod.SpecialOfferID = so.SpecialOfferID
                AND sod.OrderQty >= so.MinQty
OPTION      (MAXDOP 1, HASH JOIN)
GO

-- ------------------------------------------------------------------------------------------------------------------
-- 4. Probe residual from a nullable join column
--
-- SalesPersonID is nullable, so the plan carries a residual that simply repeats the equality. That residual is what
-- enforces NULL <> NULL. Compare the plan with query 5, which joins on a NOT NULL column and has none.
-- ------------------------------------------------------------------------------------------------------------------
SELECT      soh.SalesOrderID
           ,sp.SalesYTD
FROM        Sales.SalesPerson AS sp
            INNER JOIN Sales.SalesOrderHeader AS soh
                ON soh.SalesPersonID = sp.BusinessEntityID
OPTION      (MAXDOP 1, HASH JOIN)
GO

-- ------------------------------------------------------------------------------------------------------------------
-- 5. The same shape on a NOT NULL column, for contrast - no residual
-- ------------------------------------------------------------------------------------------------------------------
SELECT      soh.SalesOrderID
           ,st.Name
FROM        Sales.SalesTerritory AS st
            INNER JOIN Sales.SalesOrderHeader AS soh
                ON soh.TerritoryID = st.TerritoryID
OPTION      (MAXDOP 1, HASH JOIN)
GO

-- ------------------------------------------------------------------------------------------------------------------
-- 6. NULL keys on the preserved side - the drain has to find them
--
-- Three sales people have no territory. Those rows can never match, so they are only emitted once the probe is done.
-- Check the plan first: SQL Server may swap the sides and report this as a Right Outer Join.
-- ------------------------------------------------------------------------------------------------------------------
SELECT      sp.BusinessEntityID
           ,sp.TerritoryID
           ,st.Name
FROM        Sales.SalesPerson AS sp
            LEFT OUTER JOIN Sales.SalesTerritory AS st
                ON st.TerritoryID = sp.TerritoryID
OPTION      (MAXDOP 1, HASH JOIN)
GO

-- ------------------------------------------------------------------------------------------------------------------
-- 7. Left Semi Join - matched build rows, emitted once each
--
-- 266 of the 504 products have been sold. A product sold many times is still emitted once, which is what the set once
-- matched flag on the entry gives.
-- ------------------------------------------------------------------------------------------------------------------
SELECT      p.ProductID
           ,p.Name
FROM        Production.Product AS p
WHERE       EXISTS (SELECT  1
                    FROM    Sales.SalesOrderDetail AS sod
                    WHERE   sod.ProductID = p.ProductID)
OPTION      (MAXDOP 1, HASH JOIN)
GO

-- ------------------------------------------------------------------------------------------------------------------
-- 8. Left Anti Semi Join - build rows the probe never reached
-- ------------------------------------------------------------------------------------------------------------------
SELECT      p.ProductID
           ,p.Name
FROM        Production.Product AS p
WHERE       NOT EXISTS (SELECT  1
                        FROM    Sales.SalesOrderDetail AS sod
                        WHERE   sod.ProductID = p.ProductID)
OPTION      (MAXDOP 1, HASH JOIN)
GO

-- ------------------------------------------------------------------------------------------------------------------
-- 9. Full Outer Join - unmatched probe rows inline, unmatched build rows at the drain
--
-- Only hash and merge can do a full outer join, so no hint is needed to get a Hash Match.
-- ------------------------------------------------------------------------------------------------------------------
SELECT      sp.BusinessEntityID
           ,st.TerritoryID
           ,st.Name
FROM        Sales.SalesPerson AS sp
            FULL OUTER JOIN Sales.SalesTerritory AS st
                ON st.TerritoryID = sp.TerritoryID
OPTION      (MAXDOP 1)
GO

-- ------------------------------------------------------------------------------------------------------------------
-- 10. A bad estimate - the table is sized for the wrong number of rows
--
-- A leading wildcard gives the optimiser a fixed guess rather than a real estimate, so the build side estimate is
-- wrong and the table is sized from it. The summary line shows rows against what it was sized for, and the chains
-- grow accordingly.
-- ------------------------------------------------------------------------------------------------------------------
SELECT      p.Name
           ,sod.OrderQty
FROM        Production.Product AS p
            INNER JOIN Sales.SalesOrderDetail AS sod
                ON sod.ProductID = p.ProductID
WHERE       p.Name LIKE '%Frame%'
OPTION      (MAXDOP 1, HASH JOIN)
GO
