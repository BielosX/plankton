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