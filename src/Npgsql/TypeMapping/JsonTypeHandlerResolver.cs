using System;
using System.Collections.Generic;
using System.Text.Json;
using Npgsql.Internal;
using Npgsql.Internal.TypeHandlers;
using Npgsql.Internal.TypeHandling;
using Npgsql.PostgresTypes;
using NpgsqlTypes;

#if NET6_0_OR_GREATER || NETSTANDARD2_0 || NETSTANDARD2_1
using System.Text.Json.Nodes;
#endif

namespace Npgsql.TypeMapping;

sealed class JsonTypeHandlerResolver : TypeHandlerResolver
{
    readonly NpgsqlDatabaseInfo _databaseInfo;
    readonly SystemTextJsonHandler? _jsonbHandler; // Note that old version of PG (and Redshift) don't have jsonb
    readonly SystemTextJsonHandler? _jsonHandler;
    readonly Dictionary<Type, string>? _userClrTypes;

    internal JsonTypeHandlerResolver(
        NpgsqlConnector connector,
        Dictionary<Type, string>? userClrTypes,
        JsonSerializerOptions serializerOptions)
    {
        _databaseInfo = connector.DatabaseInfo;

        _jsonbHandler = new SystemTextJsonHandler(PgType("jsonb"), connector.TextEncoding, isJsonb: true, serializerOptions);
        _jsonHandler = new SystemTextJsonHandler(PgType("json"), connector.TextEncoding, isJsonb: false, serializerOptions);

        _userClrTypes = userClrTypes;
    }

    public override NpgsqlTypeHandler? ResolveByDataTypeName(string typeName)
        => typeName switch
        {
            "jsonb" => _jsonbHandler,
            "json" => _jsonHandler,
            _ => null
        };

    public override NpgsqlTypeHandler? ResolveByClrType(Type type)
        => ClrTypeToDataTypeName(type, _userClrTypes) is { } dataTypeName && ResolveByDataTypeName(dataTypeName) is { } handler
            ? handler
            : null;

    internal static string? ClrTypeToDataTypeName(Type type, Dictionary<Type, string>? clrTypes)
        => type == typeof(JsonDocument)
#if NET6_0_OR_GREATER || NETSTANDARD2_0 || NETSTANDARD2_1
           || type == typeof(JsonObject) || type == typeof(JsonArray)
#endif
            ? "jsonb"
            : clrTypes is not null && clrTypes.TryGetValue(type, out var dataTypeName) ? dataTypeName : null;

    public override TypeMappingInfo? GetMappingByDataTypeName(string dataTypeName)
        => DoGetMappingByDataTypeName(dataTypeName);

    internal static TypeMappingInfo? DoGetMappingByDataTypeName(string dataTypeName)
        => dataTypeName switch
        {
            "jsonb" => new(NpgsqlDbType.Jsonb,     "jsonb", typeof(JsonDocument)
#if NET6_0_OR_GREATER || NETSTANDARD2_0 || NETSTANDARD2_1
                , typeof(JsonObject), typeof(JsonArray)
#endif
            ),
            "json" => new(NpgsqlDbType.Json,    "json"),
            _ => null
        };

    public override NpgsqlTypeHandler? ResolveValueTypeGenerically<T>(T value)
    {
        if (typeof(T) == typeof(JsonDocument))
            return _jsonbHandler;
#if NET6_0_OR_GREATER || NETSTANDARD2_0 || NETSTANDARD2_1
        if (typeof(T) == typeof(JsonObject) || typeof(T) == typeof(JsonArray))
            return _jsonbHandler;
#endif

        return null;
    }

    PostgresType PgType(string pgTypeName) => _databaseInfo.GetPostgresTypeByName(pgTypeName);
}
