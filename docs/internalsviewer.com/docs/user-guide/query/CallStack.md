# Call Stack

Enable **Call Stack** in the [Events menu](/docs/user-guide/query#events-menu) and every captured event carries the SQL Server call stack that produced it - the chain of internal engine functions that were executing at the moment the event fired. Internals Viewer combines the call stacks from every captured event into a single merged tree for the query - the **Call Tree**. For a run recorded with [Full Trace](/docs/user-guide/query/FullTrace) the tree holds every call the query made instead, with a count on each path. Each frame then shows the memory allocated beneath it, and the [Arguments](#arguments) tab lists the values each call was passed.

The tree decodes one row per frame:

- **Module** badge - which part of SQL Server the frame belongs to (Storage Engine, Query Processor, SQL OS, etc.)
- **Category** badge - what the frame is doing, classified by Internals Viewer (Query Operator, Row Access, Index Access, Page Access, Latching, Buffer Pool, etc.)
- **Symbol** - the function itself as `module!Class::Method`, resolved from SQL Server's debugging symbols, with the offset of the call within the function
- **Target** badge - on an operator's frames, the table or index the operator works on, so two scans or seeks running the same code can be told apart. Its tooltip names the operator and its node in the plan

The **Activity** column, on by default, shows a small histogram for each frame of when that function was active across the query - a picture of whether it was a one-off or ran throughout - with the selection's time highlighted. Drag the column's edge to resize it. **Signature** adds each function's parameter types from the symbols.

The **Call Tree** tab is laid out as a dock. It starts with the tree on its own, with **Arguments** beside it while [Full Trace](/docs/user-guide/query/FullTrace) is on. **Members** and **Symbols** are added from the **Call Tree** submenu of the View menu, or when something opens them: **List Members** or **Search symbols**. Drag a tab to move it to another group or to an edge. The panels beside the tree can be closed, which also clears them on the View menu, and come back when something opens them.

<!-- Screenshot (new): the Call Tree tab's dock - the tree on the left, Members, Symbols and Arguments on the right -->

An operator row marked **no stack** is one whose frames could not be found - the operator is inlined into its parent, captured no events of its own, or its iterator class is not in the mappings. It keeps its place in the plan but has nothing beneath it.

The tree gives visibility into the engine functions behind each event. Reading down the stack for a single page read during a scan, for example: the row scanner moving to the next row, the index page manager fetching the next page, the buffer pool getting the page, and a latch suspending while the I/O completes - how the operator actually executed, not just that it read a page.

## Focus

![Call Tree with Focus on](/docs/user-guide/images/query-view-call-tree.png)

**Focus**, on by default, crops the tree to the event or operator currently selected - via the [Timeline](/docs/user-guide/query/Timeline), the [Execution Plan](/docs/user-guide/query/ExecutionPlan), or the Events pane. The header shows the selected event as a chip, with a **←** link to the right showing its parent (e.g. the statement it belongs to) - click it to navigate up to the parent's call stack. The **Back** and **Forward** buttons on the command bar, or the back and forward buttons on the mouse anywhere in the tab, undo/redo this navigation. Each step returns the tree with the frames that were open and the row that was selected, and the call that was selected in **Arguments**. Selecting a call in **Arguments** is a step of its own.

Turning Focus off shows the full call tree for the whole query, from the top-level statement execution down:

![Call Tree with Focus off](/docs/user-guide/images/call-tree-focus-off.png)

## Search and navigation

The search box filters the tree to matching frames and the calls that lead to them. A match keeps everything beneath it, collapsed, so its callees are still there to open. The tree opens down to each match, and further only where there is a deeper match. Clearing the search keeps the selected frame, with the tree open down to it.

For a [Full Trace](/docs/user-guide/query/FullTrace) run, **Batches** and **Rows** beside the search box filter the tree to the frames that handed back [batches](#batches) or rows, and the calls that lead to them. With both on the tree keeps either, and with both off it is whole again. They work alongside the search, keeping only the frames that pass both.

Going to a call - from **Arguments**, a **Find** tab or the [Flame Chart](/docs/user-guide/query/FlameChart) - turns Focus off, selects the frame and opens the tree down to it. Nothing else is hidden, so the rest of the tree stays where it was. A frame the tree does not normally show because its category is hidden is shown dimmed along with the calls that lead to it. The dimmed frames go again when you select something outside them. With **Crop To Query** on, the tree stays cropped. A call that ran outside the query, such as one found by **Find** during compilation, opens in **Arguments** but is not added to the tree.

Right-clicking a node gives:

- **Expand All** / **Collapse All** - from that node down
- **Filter To** - crops the tree to that node and everything beneath it, with the node shown in the header as **Filtered To**. **Clear Filter**, in the menu or beside the header, puts the tree back
- **Copy to Clipboard** - copies the frame's symbol as `module!Class::Method`
- **Copy Call Tree to Clipboard** - copies a formatted, nested text representation of the stack from that node down
- **List Members** - lists the members the symbols declare on the frame's class
- **Show Arguments** - for a Full Trace run, lists every call of the function with its arguments and return value - see [Arguments](#arguments)

A frame whose symbol did not resolve is copied as `module+offset`. Right-clicking a member in the Members pane gives **Copy Signature** and **Copy Symbol** - the member as `module!Class::Member`, without its parameters.

## Filtering by category

The filter button beside the search box opens a row of category badges, one for each category found in the tree. Click a badge to hide or show that category, **All** to show or hide every one at once, and **Defaults** to go back to the categories hidden by default. Those are the infrastructure categories - Compilation, Execution Tree, Expression Evaluation, Metadata, Networking, Query Binding, Query Store, Security, Tracing and Transaction Management - so the frames doing the query's work stand out. The choice is remembered between sessions, and the [Flame Chart](/docs/user-guide/query/FlameChart#search)'s search bar shares it.

![The category badges open beneath the search box, with the hidden categories struck through](/docs/user-guide/images/call-stack-category-filter.png)

## Searching Symbols

**Search symbols** on the Trace menu opens the **Symbols** tab. Typing three or more characters searches every public symbol in the modules the query's call stacks came from, so a function or class that never appeared in a stack can still be found. Each result has the same right-click actions as a member, to copy its signature or symbol. The first search of a module packs its PDB into an index, which takes a few seconds, and later searches are immediate.

The search box offers the symbol categories and plan operators from the call stack mappings as starting points: an empty box lists them all, categories first, and typing narrows them, so `Hash` offers **Hash Match**, **Hash Match (Aggregate)** and the build and probe phases. Choosing one puts its frame pattern in the box, such as `sqlmin!CQScanHash*`. A category, such as **Category: Query Operator**, searches every class and function the mappings file assigns to it. An operator with several implementations gets them all, joined with `|`: Sort searches `sqlmin!CBpQScanSort*|sqlmin!CQScanSort*|sqlmin!CQScanTopSort*`, and `|` works the same way in anything typed by hand, as an OR of searches.

Plain text matches anywhere. Text with `*` or `?` is a pattern in WinDbg's style, matched against the whole name or signature, so `*XeSqlPkg::vector*` finds that class's members and `*::GetRow` every function of that name. A `module!` prefix confines the search to that module, so a symbol pasted from WinDbg such as `sqlmin!CBpQScanColumnStoreScan::BpGetNextBatch` finds exactly that function. The **Module**, **Class** and **Signature** toggles beside the box choose which parts are matched. None ticked searches all of them. Class matches the class name alone, and Signature reaches the parameter types, so `PageId` with Signature ticked lists the functions that take one.

Results are a tree of module, then class, then the members found under it. Right-clicking a module gives **Expand all** and **Collapse all**, and a class **Expand All** and **Collapse All**. A module's menu also has **Exclude module**, which leaves that module out of every search from then on, and **Clear module exclusions**. The exclusions are kept in Settings, and while any are in force the last row of the results is a **Clear exclusions** link, with the excluded modules in its tooltip, so the way back stays in reach even when every module is excluded. The matched text is highlighted in each row, except where the row is exactly the text searched for.

Results are capped per module. A search needs a query with Call Stack events to have run first, since that is how the modules and their exact symbol files are known.

## Arguments

A [Full Trace](/docs/user-guide/query/FullTrace) logs every call it replays, with the values in the argument registers when the call was made and the return value when it came back. Right-click a frame and choose **Show Arguments** to open them on the **Arguments** tab. Until a frame is chosen the tab reads "Right click a frame in a Full Trace call tree and choose Show Arguments".

The header gives the function's signature, with its return type. Below it a table lists every call - **Call**, **Thread**, a column for each argument named from the signature (`this` first for a member function) and **Return**. Each argument's column header gives its type and the register it was read from. An iterator's calls are kept apart per object, so that two operators of the same class stay separate, and for an iterator method the table holds the calls on that frame's object only.

Selecting a call outlines it in the [Flame Chart](/docs/user-guide/query/FlameChart), and clicking a bar in the Flame Chart, or choosing **Open Call Tree** on its right-click menu, selects its call here.

![The Arguments tab beside a Full Trace call tree, listing the calls of CQueryScan::GetRow on one iterator](/docs/user-guide/images/call-stack-arguments.png)

<!-- Screenshot (update): the Arguments tab - the signature header, and the column headers with each argument's type and register -->

The four integer argument registers - RCX, RDX, R8 and R9 - are captured at the call, and RAX at the return. For functions in sqlmin and sqldk, the fifth to eighth arguments, which are passed on the stack, are read at the call too, and each one's column gives its place on the stack, such as `[RSP+0x28]`. Elsewhere those columns read **Not Captured**. Floating point values, which travel in the XMM registers, are left out, as is a floating point return value and anything after the eighth argument. A call the recording never saw return shows **Did Not Return**.

Some values are read further than the register:

- **Pages, rows and columnstore objects** - an argument that points at a page id, a row id or a columnstore object key, or at a handle or reference to a page in the buffer pool, shows the page, row or object instead of the address. Click a page to open it.
- **Log sequence numbers** - an argument that points at an `LSN` shows the LSN, such as `(37:152:2)`, in the same form as a page header's `m_lsn`.
- **Strings** - a `char const *` or `wchar_t const *` argument shows the text it points at, such as `dbo.Orders`, in the string colour the query editor uses. Only the start of a long string is read, and it is marked **Truncated**. Text the recording never read is taken from the SQL Server file it was compiled into, such as a source file name passed to an allocation. Any other text the recording had not seen shows as the address.
- **Data types** - an argument that points at a `CTypeInfo`, SQL Server's description of a column or expression type, shows the type as it is written in T-SQL, such as `nvarchar(128)`, `decimal(38,0)` or `int NOT NULL`.
- **Values behind a pointer** - a pointer or reference to a simple value, such as a number, a flag or an enum, shows the value it points at rather than the address.
- **Tables and indexes** - a HoBt id is followed by the table and index it belongs to. So is the address of a `HoBtAccess`, the object SQL Server uses to read one table or index, whether it is the `this` of a call or passed as an argument. The trace knows which HoBt each one was set up for, from its `Init` call to its destructor, and `HoBtAccess::Init`'s full HoBt id parameter is named **hobt_id**.
- **Iterators** - the address of an iterator, or of the plan object it was built from, is followed by the operator it was matched to.
- **Objects** - the trace keeps a map of what each address holds, from the calls that allocated memory there, constructed an object there, or first called a member function on it. A value is shown with the class of the object its address held at the time of the call, such as **CQScanRangeNew** followed by the address, until something else takes the address over. Memory allocated for an object shows its class from the moment it is allocated, before any member function is called on it. An address on the stack holds its object only while the function it belongs to is running. Memory from before the recording takes the class of the first member function called on it.
- **Other pointers** - a pointer with no object known at that moment shows the class its declared type points to, such as **LckInfo** for a `LckInfo const *`. A pointer to memory with no object in it, such as an `unsigned __int64 *`, shows the call that allocated the memory, such as **OperatorNew**.

**Decode**, on the tab strip, switches the table between what the values stand for and their hex. Decoded, a value shows its reading - a table and index, an operator, a page, a name or a number. A pointer to another object, such as a `HoBtAccess` or an iterator, is shown with its class first, in the class colour, as `HoBtAccess dbo.T.PK`. Pages, rows and names are shown as they are, and `this` without its class, since the function already names it. A value with nothing to decode stays in hex. The setting is shared with the [Find](#finding-a-value) tabs and remembered between sessions.

Calls the [category filter](#filtering-by-category) leaves out of the call tree are left out here too. A hidden category drops the calls of its own functions, and a category hidden with its callees drops every call made beneath it. With **Crop To Query** on, calls made outside the query, such as those made while it was compiled, are left out as well. A call reached by following an object to where it was created is listed even when it would otherwise be left out.

Decoded, an object whose address is in the trace's map is a link. Clicking it goes to the call that created the object, or to the first call made on it when it was created before the recording began. The call tree selects that function and **Arguments** selects that call.

Right-click the signature above the table and choose **Copy Signature** to copy it.

Right-click a cell for what can be done with its call and value:

- **Go To Call** - shows the call in the call tree and the Flame Chart
- **Call Stack** - opens a tab beside **Arguments** listing the calls in progress at the start of the call, on every thread, as the Flame Chart's [Call Stack](/docs/user-guide/query/FlameChart#call-stack) does. The tab stays at that point, so several can be open side by side. Clicking a call in it selects the call in the Flame Chart without leaving the tab
- **Row Buffers** - for a function that handed back rows, opens its **Row Buffers** tab, the same as the red square beside it in the call tree
- **Batches** - for a batch mode operator that handed back batches, opens its **Batches** tab, the same as the blue square beside it in the call tree
- **Scope** - scopes the Flame Chart to the call's function
- **Go To Origin** - for an object in the trace's map, goes to the call that created it, the same as clicking the link
- **Open In The Columnstore Viewer** - for a columnstore object
- **Search Call Tree** - for a page, a row or a columnstore object, searches the call tree for the frames that touched it
- **Open The Index** - for a HoBt id or a `HoBtAccess`, opens the index it belongs to
- **Show The** iterator - for an iterator's address, names the operator it was matched to, such as **Show The Clustered Index Seek (Node 3) Iterator**, or the iterator's class when it was not matched, and selects it in the call tree and the plan. So the `this` of a `GetRow` call says which operator it belongs to
- **Find** - for a pointer, a `this` or a large value, searches the whole log for it

## Batches

A batch mode operator hands each batch to the operator above it through `BpGetBatchData`. Full Trace reads the batch at that point, so the same batch is read again by each operator it passes through, and each read shows what that operator handed on. A Filter's read has the narrower selection, and a Compute Scalar's read has the vector it added. An operator fills a batch in its `BpGetNextBatch` call and hands it over in the `BpGetBatchData` call that follows, so the `BpGetNextBatch` frame shows the same batch.

A blue square beside a call tree frame means the operator handed back batches. Click it, or choose **Batches** from the frame's or a call's right-click menu, to open the operator's **Batches** tab. It lists one row per batch: the **Call** that handed it back, the **Thread**, the **Batch Size** - the most rows the batch can hold - and how many **Vectors** and **Selected** rows it held. A batch is one object for its whole journey up the plan, so each operator's read is linked to the read of the operator below it, and **Selected** shows the rows it kept out of the rows it was given, such as **10/525** for a Filter that kept 10. Selecting a row selects its call, the same as an **Arguments** row, and right-clicking it gives **Call Stack**.

Selecting a row also shows its batch beneath the list, laid out like the batch view in a simulation. Drag the splitter between them to change their heights. A batch is only read when its row is selected:

- **Data Vectors** - a column per vector, in the order the operator above asked for them, with a row for every row the operator was given. Each column header shows the vector's number, and below it the operator's output column it holds, or **Column** and its number when it could not be found. Rows the operator removed from the selection are tinted red, and rows that were not selected when it was given the batch are shaded. A row past the last one the operator kept is read from the read of the operator below it, which the cell's details say
- **Selection Vector** - the row numbers the batch has selected, in order. A Filter narrows a batch by shortening this list rather than moving any data, so its read of a batch has fewer entries than the read from the operator below it
- **Deep Data** - every value too large to fit its slot, with the vector and row it belongs to, its address, length and bytes

A vector holding one value for every row has a green **Pure** badge in its header and its cells are tinted. Only the first row holds the value, so the others are dimmed.

Each cell shows the normalized slot as it was read, or with **Decode** on, the value it stands for. A NULL shows as `NULL`, a value read from deep data shows as that value, and a dictionary reference shows as its dictionary and entry, such as **Dictionary 128 Entry 230**. Dictionary 0 is the query's own dictionary, and from 128 up they are the column store's dictionaries. Click a cell for its details beneath the table: the slot in hex and as a number, its type, the column's data type and normalization domain, and the decoded value, plus the dictionary and entry, or the deep data bytes.

## Finding a value

**Find** opens a tab for the value beside **Arguments**, named after the value, with one tab per value. The tab lists everywhere the value appears in the log, under the heading **References To** and the value, as a tree. It searches every call in the trace, including those the category filter and **Crop To Query** leave out of the call tree, so following a value never loses part of its trail:

- **Type** - what the value is, where it can be identified: **Hash Match (Aggregate) (Node 1) Iterator** for an iterator, the operator and **Plan Object** for the object in the compiled plan that SQL Server builds an operator's iterator from, **HoBtAccess** and its table and index for an access object, the class of the object it held when it was first used, such as **CQScanRangeNew**, or otherwise the class of the functions called on it. Clicking an iterator or plan object selects it in the call tree and the plan
- **Called by** - member functions called with the value as `this`
- **Returned by** - functions that returned it
- **Referenced by** - functions it was passed to as an argument

Each group gives its number of functions and calls, and lists the functions with the register the value was in. Expanding a function lists its calls, loaded as you open it, with **Show More** for the rest.

Each call reads as the function's signature with the values it was passed in place of its parameters, then the value it returned after the arrow. A parameter the trace did not capture shows its type, and hovering over a value gives its type and register. When the function's signature cannot be resolved, the four argument registers and RAX are shown instead.

**Decode** works here as it does in the Arguments table, and the two share the setting. Decoded readings are in the normal font and hex in monospace, and hovering over a value shows its other form.

Right-click a value in a call and choose **Find** to open a tab for that value in turn. Clicking a call shows it in the call tree and the [Flame Chart](/docs/user-guide/query/FlameChart), and selects it in **Arguments**. Following a pointer like this traces an object through the query - what created it, what was done to it and what it was handed to - and then the same for every object it met along the way. When nothing matches, the heading reads **No References To** and the value.

<!-- Screenshot (new): a Find tab beside Arguments - Type, Called by and Referenced by, with one function expanded to its calls, each a signature with its values in place -->

## Flame Graph

When Call Stack events are captured, the [Execution Plan](/docs/user-guide/query/ExecutionPlan) can optionally display a Flame Graph - an icicle chart of the calls per operator.

## Symbols

Turning stack frames into function names requires the debugging symbols (PDB files) for the exact SQL Server build being traced. Internals Viewer handles this automatically: the first time call stacks are processed, the required symbol files are downloaded from the Microsoft public symbol server and cached locally, and later traces resolve straight from the cache. Download progress is shown in Messages. There is nothing to install or configure - no debugging tools and no symbol server setup.

The cache location is the **Symbols Path** [setting](/docs/user-guide/settings#symbols-path). The default is `C:\Symbols`.

See [How query tracing works](/docs/deep-dives/query-tracing#resolving-call-stacks) for how the download and resolution work.
