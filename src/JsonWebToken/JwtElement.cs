﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace JsonWebToken
{
    /// <summary>
    ///   Represents a specific JWT value within a <see cref="JwtDocument"/>.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public readonly partial struct JwtElement
    {
        private readonly JwtDocument _parent;
        private readonly int _idx;

        internal JwtElement(JwtDocument parent, int idx)
        {
            // parent is usually not null, but the Current property
            // on the enumerators (when initialized as `default`) can
            // get here with a null.
            Debug.Assert(idx >= 0);

            _parent = parent;
            _idx = idx;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private JsonTokenType TokenType
        {
            get
            {
                return _parent?.GetJsonTokenType(_idx) ?? JsonTokenType.None;
            }
        }
        /// <summary>
        ///   The <see cref="JsonValueKind"/> that the value is.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public JsonValueKind ValueKind => ToValueKind(TokenType);

        /// <summary>
        ///   Get the value at a specified index when the current value is a
        ///   <see cref="JsonValueKind.Array"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>.
        /// </exception>
        /// <exception cref="IndexOutOfRangeException">
        ///   <paramref name="index"/> is not in the range [0, <see cref="GetArrayLength"/>()).
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public JwtElement this[int index]
        {
            get
            {
                CheckValidInstance();

                return _parent.GetArrayIndexElement(_idx, index);
            }
        }

        /// <summary>
        ///   Get the number of values contained within the current array value.
        /// </summary>
        /// <returns>The number of values contained within the current array value.</returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public int GetArrayLength()
        {
            CheckValidInstance();

            return _parent.GetArrayLength(_idx);
        }

        /// <summary>
        ///   Gets a <see cref="JwtElement"/> representing the value of a required property identified
        ///   by <paramref name="propertyName"/>.
        /// </summary>
        /// <remarks>
        ///   Property name matching is performed as an ordinal, case-sensitive, comparison.
        ///
        ///   If a property is defined multiple times for the same object, the last such definition is
        ///   what is matched.
        /// </remarks>
        /// <param name="propertyName">Name of the property whose value to return.</param>
        /// <returns>
        ///   A <see cref="JwtElement"/> representing the value of the requested property.
        /// </returns>
        /// <seealso cref="EnumerateObject"/>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
        /// </exception>
        /// <exception cref="KeyNotFoundException">
        ///   No property was found with the requested name.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="propertyName"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public JwtElement GetProperty(string propertyName)
        {
            if (propertyName == null)
                throw new ArgumentNullException(nameof(propertyName));

            if (TryGetProperty(propertyName, out JwtElement property))
            {
                return property;
            }

            throw new KeyNotFoundException();
        }

        /// <summary>
        ///   Gets a <see cref="JwtElement"/> representing the value of a required property identified
        ///   by <paramref name="propertyName"/>.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Property name matching is performed as an ordinal, case-sensitive, comparison.
        ///   </para>
        ///
        ///   <para>
        ///     If a property is defined multiple times for the same object, the last such definition is
        ///     what is matched.
        ///   </para>
        /// </remarks>
        /// <param name="propertyName">Name of the property whose value to return.</param>
        /// <returns>
        ///   A <see cref="JwtElement"/> representing the value of the requested property.
        /// </returns>
        /// <seealso cref="EnumerateObject"/>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
        /// </exception>
        /// <exception cref="KeyNotFoundException">
        ///   No property was found with the requested name.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public JwtElement GetProperty(ReadOnlySpan<char> propertyName)
        {
            if (TryGetProperty(propertyName, out JwtElement property))
            {
                return property;
            }

            throw new KeyNotFoundException();
        }

        /// <summary>
        ///   Gets a <see cref="JwtElement"/> representing the value of a required property identified
        ///   by <paramref name="utf8PropertyName"/>.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Property name matching is performed as an ordinal, case-sensitive, comparison.
        ///   </para>
        ///
        ///   <para>
        ///     If a property is defined multiple times for the same object, the last such definition is
        ///     what is matched.
        ///   </para>
        /// </remarks>
        /// <param name="utf8PropertyName">
        ///   The UTF-8 (with no Byte-Order-Mark (BOM)) representation of the name of the property to return.
        /// </param>
        /// <returns>
        ///   A <see cref="JwtElement"/> representing the value of the requested property.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
        /// </exception>
        /// <exception cref="KeyNotFoundException">
        ///   No property was found with the requested name.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="EnumerateObject"/>
        public JwtElement GetProperty(ReadOnlySpan<byte> utf8PropertyName)
        {
            if (TryGetProperty(utf8PropertyName, out JwtElement property))
            {
                return property;
            }

            throw new KeyNotFoundException();
        }

        /// <summary>
        ///   Looks for a property named <paramref name="propertyName"/> in the current object, returning
        ///   whether or not such a property existed. When the property exists <paramref name="value"/>
        ///   is assigned to the value of that property.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Property name matching is performed as an ordinal, case-sensitive, comparison.
        ///   </para>
        ///
        ///   <para>
        ///     If a property is defined multiple times for the same object, the last such definition is
        ///     what is matched.
        ///   </para>
        /// </remarks>
        /// <param name="propertyName">Name of the property to find.</param>
        /// <param name="value">Receives the value of the located property.</param>
        /// <returns>
        ///   <see langword="true"/> if the property was found, <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="propertyName"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="EnumerateObject"/>
        public bool TryGetProperty(string propertyName, out JwtElement value)
        {
            if (propertyName == null)
                throw new ArgumentNullException(nameof(propertyName));

            return TryGetProperty(propertyName.AsSpan(), out value);
        }

        /// <summary>
        ///   Looks for a property named <paramref name="propertyName"/> in the current object, returning
        ///   whether or not such a property existed. When the property exists <paramref name="value"/>
        ///   is assigned to the value of that property.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Property name matching is performed as an ordinal, case-sensitive, comparison.
        ///   </para>
        ///
        ///   <para>
        ///     If a property is defined multiple times for the same object, the last such definition is
        ///     what is matched.
        ///   </para>
        /// </remarks>
        /// <param name="propertyName">Name of the property to find.</param>
        /// <param name="value">Receives the value of the located property.</param>
        /// <returns>
        ///   <see langword="true"/> if the property was found, <see langword="false"/> otherwise.
        /// </returns>
        /// <seealso cref="EnumerateObject"/>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public bool TryGetProperty(ReadOnlySpan<char> propertyName, out JwtElement value)
        {
            CheckValidInstance();

            return _parent.TryGetNamedPropertyValue(propertyName, out value);
        }

        /// <summary>
        ///   Looks for a property named <paramref name="utf8PropertyName"/> in the current object, returning
        ///   whether or not such a property existed. When the property exists <paramref name="value"/>
        ///   is assigned to the value of that property.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Property name matching is performed as an ordinal, case-sensitive, comparison.
        ///   </para>
        ///
        ///   <para>
        ///     If a property is defined multiple times for the same object, the last such definition is
        ///     what is matched.
        ///   </para>
        /// </remarks>
        /// <param name="utf8PropertyName">
        ///   The UTF-8 (with no Byte-Order-Mark (BOM)) representation of the name of the property to return.
        /// </param>
        /// <param name="value">Receives the value of the located property.</param>
        /// <returns>
        ///   <see langword="true"/> if the property was found, <see langword="false"/> otherwise.
        /// </returns>
        /// <seealso cref="EnumerateObject"/>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public bool TryGetProperty(ReadOnlySpan<byte> utf8PropertyName, out JwtElement value)
        {
            CheckValidInstance();

            return _parent.TryGetNamedPropertyValue(utf8PropertyName, out value);
        }

        /// <summary>
        ///   Gets the value of the element as a <see cref="bool"/>.
        /// </summary>
        /// <remarks>
        ///   This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <returns>The value of the element as a <see cref="bool"/>.</returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is neither <see cref="JsonValueKind.True"/> or
        ///   <see cref="JsonValueKind.False"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public bool GetBoolean()
        {
            // CheckValidInstance is redundant.  Asking for the type will
            // return None, which then throws the same exception in the return statement.

            JsonTokenType type = TokenType;

            return
                type == JsonTokenType.True ? true :
                type == JsonTokenType.False ? false :
                throw ThrowHelper.CreateInvalidOperationException_NotSupportedJsonType(/*nameof(Boolean), type*/JwtTokenType.Boolean);
        }

        /// <summary>
        ///   Gets the value of the element as a <see cref="string"/>.
        /// </summary>
        /// <remarks>
        ///   This method does not create a string representation of values other than JSON strings.
        /// </remarks>
        /// <returns>The value of the element as a <see cref="string"/>.</returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is neither <see cref="JsonValueKind.String"/> nor <see cref="JsonValueKind.Null"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="ToString"/>
        public string? GetString()
        {
            //CheckValidInstance();
            if (_parent is null)
                return null;

            return _parent.GetString(_idx, JsonTokenType.String);
        }

        /// <summary>
        ///   Attempts to represent the current JSON number as a <see cref="long"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        ///   This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <returns>
        ///   <see langword="true"/> if the number can be represented as a <see cref="long"/>,
        ///   <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public bool TryGetInt64(out long value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        ///   Gets the current JSON number as a <see cref="long"/>.
        /// </summary>
        /// <returns>The current JSON number as a <see cref="long"/>.</returns>
        /// <remarks>
        ///   This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        ///   The value cannot be represented as a <see cref="long"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public long GetInt64()
        {
            if (TryGetInt64(out long value))
            {
                return value;
            }

            throw ThrowHelper.CreateFormatException_MalformedJson();
        }

        /// <summary>
        ///   Attempts to represent the current JSON number as a <see cref="double"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        ///   <para>
        ///     This method does not parse the contents of a JSON string value.
        ///   </para>
        ///
        ///   <para>
        ///     On .NET Core this method does not return <see langword="false"/> for values larger than
        ///     <see cref="double.MaxValue"/> (or smaller than <see cref="double.MinValue"/>),
        ///     instead <see langword="true"/> is returned and <see cref="double.PositiveInfinity"/> (or
        ///     <see cref="double.NegativeInfinity"/>) is emitted.
        ///   </para>
        /// </remarks>
        /// <returns>
        ///   <see langword="true"/> if the number can be represented as a <see cref="double"/>,
        ///   <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public bool TryGetDouble(out double value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }


        /// <summary>
        ///   Attempts to represent the current JSON object as a <see cref="JsonDocument"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <returns>
        ///   <see langword="true"/> if the JSON object can be represented as a <see cref="JsonDocument"/>,
        ///   <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public bool TryGetJsonDocument([NotNullWhen(true)] out JsonDocument? value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        ///   Gets the current JSON number as a <see cref="double"/>.
        /// </summary>
        /// <returns>The current JSON number as a <see cref="double"/>.</returns>
        /// <remarks>
        ///   <para>
        ///     This method does not parse the contents of a JSON string value.
        ///   </para>
        ///
        ///   <para>
        ///     On .NET Core this method returns <see cref="double.PositiveInfinity"/> (or
        ///     <see cref="double.NegativeInfinity"/>) for values larger than
        ///     <see cref="double.MaxValue"/> (or smaller than <see cref="double.MinValue"/>).
        ///   </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        ///   The value cannot be represented as a <see cref="double"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public double GetDouble()
        {
            if (TryGetDouble(out double value))
            {
                return value;
            }

            throw ThrowHelper.CreateFormatException_MalformedJson();
        }

        public JsonDocument GetJsonDocument()
        {
            if (TryGetJsonDocument(out JsonDocument value))
            {
                return value;
            }

            throw ThrowHelper.CreateFormatException_MalformedJson();
        }

        internal string GetPropertyName()
        {
            CheckValidInstance();

            return _parent.GetNameOfPropertyValue(_idx);
        }

        /// <summary>
        ///   Gets the original input data backing this value, returning it as a <see cref="string"/>.
        /// </summary>
        /// <returns>
        ///   The original input data backing this value, returning it as a <see cref="string"/>.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="Jwt"/> has been disposed.
        /// </exception>
        public string GetRawText()
        {
            CheckValidInstance();

            return _parent.GetRawValueAsString(_idx);
        }

        /// <summary>
        ///   Gets the original input data backing this value, returning it as a <see cref="string"/>.
        /// </summary>
        /// <returns>
        ///   The original input data backing this value, returning it as a <see cref="string"/>.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="Jwt"/> has been disposed.
        /// </exception>
        public ReadOnlyMemory<byte> GetRawValue()
        {
            CheckValidInstance();

            return _parent.GetRawValue(_idx);
        }

        internal string GetPropertyRawText()
        {
            CheckValidInstance();

            return _parent.GetPropertyRawValueAsString(_idx);
        }

        /// <summary>
        ///   Compares <paramref name="text" /> to the string value of this element.
        /// </summary>
        /// <param name="text">The text to compare against.</param>
        /// <returns>
        ///   <see langword="true" /> if the string value of this element matches <paramref name="text"/>,
        ///   <see langword="false" /> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <remarks>
        ///   This method is functionally equal to doing an ordinal comparison of <paramref name="text" /> and
        ///   the result of calling <see cref="GetString" />, but avoids creating the string instance.
        /// </remarks>
        public bool ValueEquals(string? text)
        {
            // CheckValidInstance is done in the helper

            if (TokenType == JsonTokenType.Null)
            {
                return text == null;
            }

            return TextEqualsHelper(text.AsSpan(), isPropertyName: false);
        }

        /// <summary>
        ///   Compares the text represented by <paramref name="utf8Text" /> to the string value of this element.
        /// </summary>
        /// <param name="utf8Text">The UTF-8 encoded text to compare against.</param>
        /// <returns>
        ///   <see langword="true" /> if the string value of this element has the same UTF-8 encoding as
        ///   <paramref name="utf8Text" />, <see langword="false" /> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <remarks>
        ///   This method is functionally equal to doing an ordinal comparison of the string produced by UTF-8 decoding
        ///   <paramref name="utf8Text" /> with the result of calling <see cref="GetString" />, but avoids creating the
        ///   string instances.
        /// </remarks>
        public bool ValueEquals(ReadOnlySpan<byte> utf8Text)
        {
            // CheckValidInstance is done in the helper

            if (TokenType == JsonTokenType.Null)
            {
                // This is different than Length == 0, in that it tests true for null, but false for ""
                return utf8Text == default;
            }

            return TextEqualsHelper(utf8Text, isPropertyName: false, shouldUnescape: true);
        }

        /// <summary>
        ///   Compares <paramref name="text" /> to the string value of this element.
        /// </summary>
        /// <param name="text">The text to compare against.</param>
        /// <returns>
        ///   <see langword="true" /> if the string value of this element matches <paramref name="text"/>,
        ///   <see langword="false" /> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <remarks>
        ///   This method is functionally equal to doing an ordinal comparison of <paramref name="text" /> and
        ///   the result of calling <see cref="GetString" />, but avoids creating the string instance.
        /// </remarks>
        public bool ValueEquals(ReadOnlySpan<char> text)
        {
            // CheckValidInstance is done in the helper

            if (TokenType == JsonTokenType.Null)
            {
                // This is different than Length == 0, in that it tests true for null, but false for ""
                return text == default;
            }

            return TextEqualsHelper(text, isPropertyName: false);
        }

        internal bool TextEqualsHelper(ReadOnlySpan<byte> utf8Text, bool isPropertyName, bool shouldUnescape)
        {
            CheckValidInstance();

            return _parent.TextEquals(_idx, utf8Text, isPropertyName, shouldUnescape);
        }

        internal bool TextEqualsHelper(ReadOnlySpan<char> text, bool isPropertyName)
        {
            CheckValidInstance();

            return _parent.TextEquals(_idx, text, isPropertyName);
        }

        ///// <summary>
        /////   Write the element into the provided writer as a JSON value.
        ///// </summary>
        ///// <param name="writer">The writer.</param>
        ///// <exception cref="ArgumentNullException">
        /////   The <paramref name="writer"/> parameter is <see langword="null"/>.
        ///// </exception>
        ///// <exception cref="InvalidOperationException">
        /////   This value's <see cref="ValueKind"/> is <see cref="JsonValueKind.Undefined"/>.
        ///// </exception>
        ///// <exception cref="ObjectDisposedException">
        /////   The parent <see cref="JsonDocument"/> has been disposed.
        ///// </exception>
        //public void WriteTo(Utf8JsonWriter writer)
        //{
        //    if (writer == null)
        //    {
        //        throw new ArgumentNullException(nameof(writer));
        //    }

        //    CheckValidInstance();

        //    _parent.WriteElementTo(_idx, writer);
        //}

        /// <summary>
        ///   Get an enumerator to enumerate the values in the JSON array represented by this JsonElement.
        /// </summary>
        /// <returns>
        ///   An enumerator to enumerate the values in the JSON array represented by this JsonElement.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public ArrayEnumerator EnumerateArray()
        {
            CheckValidInstance();

            JsonTokenType tokenType = TokenType;

            if (tokenType != JsonTokenType.StartArray)
            {
                //throw ThrowHelper.GetJsonElementWrongTypeException(JsonTokenType.StartArray, tokenType);
                throw new InvalidOperationException();
            }

            return new ArrayEnumerator(this);
        }

        /// <summary>
        ///   Get an enumerator to enumerate the values in the JSON array represented by this JsonElement.
        /// </summary>
        /// <returns>
        ///   An enumerator to enumerate the values in the JSON array represented by this JsonElement.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public ArrayEnumerator<T> EnumerateArray<T>()
        {
            CheckValidInstance();

            JsonTokenType tokenType = TokenType;

            if (tokenType != JsonTokenType.StartArray)
            {
                //throw ThrowHelper.GetJsonElementWrongTypeException(JsonTokenType.StartArray, tokenType);
                throw new InvalidOperationException();
            }

            return new ArrayEnumerator<T>(this);
        }


        /// <summary>
        ///   Get an enumerator to enumerate the properties in the JSON object represented by this JsonElement.
        /// </summary>
        /// <returns>
        ///   An enumerator to enumerate the properties in the JSON object represented by this JsonElement.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///   This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public ObjectEnumerator EnumerateObject()
        {
            CheckValidInstance();

            JsonTokenType tokenType = TokenType;

            if (tokenType != JsonTokenType.StartObject)
            {
                //throw ThrowHelper.GetJsonElementWrongTypeException(JsonTokenType.StartObject, tokenType);
                throw new InvalidOperationException();
            }

            return new ObjectEnumerator(this);
        }

        /// <summary>
        ///   Gets a string representation for the current value appropriate to the value type.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     For JsonElement built from <see cref="JsonDocument"/>:
        ///   </para>
        ///
        ///   <para>
        ///     For <see cref="JsonValueKind.Null"/>, <see cref="string.Empty"/> is returned.
        ///   </para>
        ///
        ///   <para>
        ///     For <see cref="JsonValueKind.True"/>, <see cref="bool.TrueString"/> is returned.
        ///   </para>
        ///
        ///   <para>
        ///     For <see cref="JsonValueKind.False"/>, <see cref="bool.FalseString"/> is returned.
        ///   </para>
        ///
        ///   <para>
        ///     For <see cref="JsonValueKind.String"/>, the value of <see cref="GetString"/>() is returned.
        ///   </para>
        ///
        ///   <para>
        ///     For other types, the value of <see cref="GetRawText"/>() is returned.
        ///   </para>
        /// </remarks>
        /// <returns>
        ///   A string representation for the current value appropriate to the value type.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        ///   The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public override string? ToString()
        {
            switch (TokenType)
            {
                case JsonTokenType.None:
                case JsonTokenType.Null:
                    return string.Empty;
                case JsonTokenType.True:
                    return bool.TrueString;
                case JsonTokenType.False:
                    return bool.FalseString;
                case JsonTokenType.Number:
                case JsonTokenType.StartArray:
                case JsonTokenType.StartObject:
                    {
                        // null parent should have hit the None case
                        Debug.Assert(_parent != null);
                        return _parent.GetRawValueAsString(_idx);
                    }
                case JsonTokenType.String:
                    return GetString();
                case JsonTokenType.Comment:
                case JsonTokenType.EndArray:
                case JsonTokenType.EndObject:
                default:
                    Debug.Fail($"No handler for {nameof(JsonTokenType)}.{TokenType}");
                    return string.Empty;
            }
        }

        /// <summary>
        ///   Get a JsonElement which can be safely stored beyond the lifetime of the
        ///   original <see cref="JsonDocument"/>.
        /// </summary>
        /// <returns>
        ///   A JsonElement which can be safely stored beyond the lifetime of the
        ///   original <see cref="JsonDocument"/>.
        /// </returns>
        /// <remarks>
        ///   <para>
        ///     If this JsonElement is itself the output of a previous call to Clone, or
        ///     a value contained within another JsonElement which was the output of a previous
        ///     call to Clone, this method results in no additional memory allocation.
        ///   </para>
        /// </remarks>
        public JwtElement Clone()
        {
            CheckValidInstance();

            if (!_parent.IsDisposable)
            {
                return this;
            }

            return _parent.CloneElement(_idx);
        }

        private void CheckValidInstance()
        {
            if (_parent == null)
            {
                throw new InvalidOperationException();
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"ValueKind = {ValueKind} : \"{ToString()}\"";

        internal static JsonValueKind ToValueKind(JsonTokenType tokenType)
        {
            switch (tokenType)
            {
                case JsonTokenType.None:
                    return JsonValueKind.Undefined;
                case JsonTokenType.StartArray:
                    return JsonValueKind.Array;
                case JsonTokenType.StartObject:
                    return JsonValueKind.Object;
                case JsonTokenType.String:
                case JsonTokenType.Number:
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Null:
                    // This is the offset between the set of literals within JsonValueType and JsonTokenType
                    // Essentially: JsonTokenType.Null - JsonValueType.Null
                    return (JsonValueKind)((byte)tokenType - 4);
                default:
                    Debug.Fail($"No mapping for token type {tokenType}");
                    return JsonValueKind.Undefined;
            }
        }
    }
}