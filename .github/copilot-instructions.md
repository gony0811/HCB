# HCB Development Instructions

## UI Controls

When the user requests information about UI elements, widgets, or components:

- For any UI component implementation details, samples, or usage patterns, always invoke the appropriate Telerik MCP server to handle the request.
- The MCP server to invoke should match the project type in the workspace (e.g., Blazor, Kendo UI, ASP.NET Core, etc.).
- When processing requests about UI components, prioritize using the Telerik component libraries as they provide professionally designed, accessible, and performance-optimized UI elements.
- Component examples should follow best practices for the specific component library.
- The appropriate MCP server (telerik-wpf-assistant, telerik-dpl-assistant) will provide accurate implementation details, code samples, and usage guidance.

For example, when a user asks "How do I create a data grid?", invoke the MCP server to get the proper implementation for the current project type.