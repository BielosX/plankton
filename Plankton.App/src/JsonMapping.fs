namespace Plankton.JsonMapping

open FSharp.Reflection
open System.Text.Json.Serialization
open System.Text.Json

type TupleMapper<'a>() =
    inherit JsonConverter<'a>()

    override this.CanConvert (typeToConvert: System.Type): bool = 
        FSharpType.IsTuple typeToConvert
    
    override this.Read (reader: byref<Utf8JsonReader>, typeToConvert: System.Type, options: JsonSerializerOptions): 'a = 
        raise (System.NotImplementedException())
    override this.Write (writer: Utf8JsonWriter, value: 'a, options: JsonSerializerOptions): unit = 
        writer.WriteStartArray()
        let mutable index = 0
        let tupleElements = FSharpType.GetTupleElements (value.GetType())
        for element in tupleElements do
            let valueAtIndex = FSharpValue.GetTupleField(value, index)
            JsonSerializer.Serialize(writer, valueAtIndex, element, options)
            index <- index + 1
        writer.WriteEndArray()