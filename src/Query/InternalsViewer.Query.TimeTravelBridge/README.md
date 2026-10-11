# Internals Viewer - Time Travel Debugging (TTD) Bridge

This project is a bridge for the Time Travel Debugging API and Internals Viewer to read `.run` trace files.

Internals Viewer uses TTD because it needs to capture all function calls with arguments (registers). ETW/other sample based trace tools are
not suitable and the trace needs a full capture and other tools do not have the memory capture required for argument resolution.

TTD .run files are a proprietary format that need to be decoded using the API. Results are parsed into a set of Modules and Call Tree 
nodes.

## Steps

### Open Trace

1. `Initialize()`

This loads and indexes the trace file, parsing and prepares the file for reading. This is the only way to load the trace and is an 
expensive operation, but there is no way around it as it's a mandatory step to use the API.

2. Read Modules

Modules are listed in the trace then a collection of name, start address, and size for each module is created. This is so an address can be 
mapped to a specific module. If an address is located between start address and start address + size it belongs to that module. 

To use Symbols we need RVA (Relative Virtual Address) + Module. From the module information we can go from an absolute address to a virtual
address using `Relative Virtual Address (RVA) = Absolute Address - Module Start Address`. From that we can lookup the symbols as we have
both the module and the RVA.

### Read Call Tree



