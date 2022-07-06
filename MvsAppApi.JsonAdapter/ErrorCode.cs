namespace MvsAppApi.JsonAdapter
{
    public enum ErrorCode
    {
        Success = 0,
        GeneralFailure = -1,  // default, try to avoid.  use when error is not yet classified as one of the other error codes listed below
        Restricted = 100,     // used when licensing restrictions prevent satisfying a request
        Timeout = 110,
        InvalidState = 120,

        // see http://www.jsonrpc.org/specification#error_object
        // some pre-defined json-rpc error codes follow ...  

        ParseError = -32700,        // use if request can't be deserialized as Json
        InvalidRequest = -32600,    // use if its not valid json-rpc format
        MethodNotFound = -32601,
        InvalidParams = -32602,
        InternalError = -32603      // use for arbitrary exceptions
    }
}