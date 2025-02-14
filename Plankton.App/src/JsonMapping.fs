namespace Plankton.JsonMapping

open FSharp.Reflection
open System.Text.Json.Serialization
open System.Text.Json

type TupleMapper<'a>() =
    inherit JsonConverter<'a>()

    override this.CanConvert (typeToConvert: System.Type): bool = 
        FSharpType.IsTuple typeToConvert
    
    override this.Read (reader: byref<Utf8JsonReader>, typeToConvert: System.Type, options: JsonSerializerOptions): 'a = 
        if reader.TokenType <> JsonTokenType.StartArray then
            raise (JsonException $"Array start expected, got {reader.TokenType}")
        reader.Read() |> ignore
        let tupleElements = FSharpType.GetTupleElements typeToConvert
        let valueArray: objnull array = Array.zeroCreate  tupleElements.Length
        let mutable index = 0
        for element in tupleElements do
            let value = JsonSerializer.Deserialize(&reader, element, options)
            reader.Read() |> ignore
            valueArray[index] <- value
            index <- index + 1
        reader.Read() |> ignore
        if reader.TokenType <> JsonTokenType.EndArray then
            raise (JsonException $"Array end expected, got {reader.TokenType}")
        downcast FSharpValue.MakeTuple(valueArray, typeToConvert)

    override this.Write (writer: Utf8JsonWriter, value: 'a, options: JsonSerializerOptions): unit = 
        writer.WriteStartArray()
        let mutable index = 0
        let tupleElements = FSharpType.GetTupleElements (value.GetType())
        for element in tupleElements do
            let valueAtIndex = FSharpValue.GetTupleField(value, index)
            JsonSerializer.Serialize(writer, valueAtIndex, element, options)
            index <- index + 1
        writer.WriteEndArray()

type RecordMapper<'a>() =
    inherit JsonConverter<'a>()

    override this.CanConvert (typeToConvert: System.Type): bool = 
        FSharpType.IsRecord typeToConvert

    override this.Read (reader: byref<Utf8JsonReader>, typeToConvert: System.Type, options: JsonSerializerOptions): 'a = 
        let recordElements = FSharpType.GetRecordFields(typeToConvert)
        let mutable valueArray: objnull array = Array.zeroCreate recordElements.Length
        let fieldNameToType = dict [
                for idx in 0..(recordElements.Length - 1) -> recordElements[idx].Name, (idx, recordElements[idx].PropertyType)
            ]
        if reader.TokenType <> JsonTokenType.StartObject then
            raise (JsonException $"Object start expected, got {reader.TokenType}")
        while reader.Read() && not (reader.TokenType = JsonTokenType.EndObject) do
            if reader.TokenType <> JsonTokenType.PropertyName then
                raise (JsonException $"Property name expected, got {reader.TokenType}")
            let propertyName = reader.GetString()
            let propertyType = snd fieldNameToType[propertyName]
            let propertyIndex = fst fieldNameToType[propertyName]
            let propertyValue = JsonSerializer.Deserialize(&reader, propertyType, options)
            valueArray[propertyIndex] <- propertyValue
        if reader.TokenType <> JsonTokenType.EndObject then
            raise (JsonException $"Object end expected, got {reader.TokenType}")
        downcast FSharpValue.MakeRecord(typeToConvert, valueArray)

    override this.Write (writer: Utf8JsonWriter, value: 'a, options: JsonSerializerOptions): unit = 
        writer.WriteStartObject()
        let recordElements = FSharpType.GetRecordFields(value.GetType())
        for element in recordElements do
            writer.WritePropertyName element.Name
            let value: obj = nonNull (FSharpValue.GetRecordField(value, element))
            JsonSerializer.Serialize(writer, value, element.PropertyType, options)
        writer.WriteEndObject()

type UnionMapper<'a>() =
    inherit JsonConverter<'a>()

    override this.CanConvert (typeToConvert: System.Type): bool = 
        FSharpType.IsUnion typeToConvert

    override this.Read (reader: byref<Utf8JsonReader>, typeToConvert: System.Type, options: JsonSerializerOptions): 'a = 
            raise (System.NotImplementedException())

    override this.Write (writer: Utf8JsonWriter, value: 'a, options: JsonSerializerOptions): unit = 
        let unionCase, fields = FSharpValue.GetUnionFields(value, value.GetType())
        let fieldTypes = unionCase.GetFields()
        writer.WriteStartObject()
        writer.WritePropertyName unionCase.Name
        writer.WriteStartObject()
        for idx in 0..(fieldTypes.Length - 1) do
            writer.WritePropertyName(fieldTypes[idx].Name)
            JsonSerializer.Serialize(writer, fields[idx], fieldTypes[idx].PropertyType, options)
        writer.WriteEndObject()
        writer.WriteEndObject()