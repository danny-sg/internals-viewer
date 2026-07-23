---
outline: deep
---

# Getting started

## Requirements

Internals Viewer will run on Windows 10/11 (version 17763.0 or higher).

SQL Server 2019-2025 is required when connecting to a SQL Server database or file.

`sysadmin` permissions are required for a SQL Server connection, see [Permissions](/docs/introduction/permissions).

## Installation

The easiest way to install Internals Viewer is to get it from the Microsoft Store.

<a href="https://get.microsoft.com/installer/download/9MSW42CQMK2V?referrer=appbadge" target="_self" >
	<img src="https://get.microsoft.com/images/en-us%20dark.svg" width="200"/>
</a>

To install manually see [Installation](/docs/introduction/installation.md)

## Connecting

When you open the application the Start page will bring up the different options to connect - a live SQL Server instance, a detached/offline data file, or a database backup.

![Start page](/docs/tutorial/images/screenshots/Start_page.png)

To connect to a SQL Server instance:

- Click on Connect to SQL Server
- Set Instance Name to the name of the SQL Server
- Choose either Active Directory Integrated or SQL Password for the Authentication type
- For Database either type in the name of the database or expand the drop down list to see a list of databases on the server
- Click Connect

![Connect to SQL Server](/docs/tutorial/images/screenshots/Connect_sql_server.png)
