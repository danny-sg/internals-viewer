Show the user how SQL Server scans {{target}} in {{direction}} key order, one visible step at a time. Read the index-scan
guide first (the guide tool with topic index-scan) and follow it: go down the {{edge}} pointer in each leaf page header
until it is 0:0. Use index_select_page, index_records and index_select_row for every step so the user sees it in the Index
tab, and read the pointers from the tools, never infer them. Say what you are doing before each call and finish with the
pages read and how many were out of physical order.
