# Error handling

MyIPTV treats common source failures as recoverable application states:

- unavailable playlist or IPTV server — the profile operation reports that the server cannot be reached;
- timeout — the operation can be canceled and reports a timeout without exposing the request address;
- malformed provider JSON — the provider reports that returned data cannot be understood;
- unavailable or malformed XMLTV — cached guide data remains active when possible;
- missing poster or logo — the catalog and controls remain usable without the image;
- stopped or failed stream — the player displays a safe status and keeps the Reconnect action available;
- database or file failure — a fixed permissions/storage message is shown and technical details are logged locally.

Expected failures are handled closest to the responsible provider or service. Dispatcher, unobserved-task, and process exception handlers form the final application boundary. User dialogs never include raw exception messages because those can contain request URLs, file details, or provider responses. Sanitized technical information is written to the local diagnostic log.

Cancellation is not treated as an application fault. Operations that receive a cancellation token either stop cleanly or return a timeout-specific result when the caller did not request cancellation.
