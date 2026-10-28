Show the user how SQL Server finds the key {{key}} in {{target}}, one visible step at a time. Read the index-seek
guide first (the guide tool with topic index-seek) and follow it exactly: select the root page with
index_select_page, read its records with index_records, binary search the slots and call index_select_row on each
slot you probe, then select the child page of the last key at or below the target. Repeat down to the leaf, then
open the page with show_page and select the row with page_select_slot. Read keys from index_records, never infer
them from row counts. Say what you compared before each call and finish with the number of pages read.
