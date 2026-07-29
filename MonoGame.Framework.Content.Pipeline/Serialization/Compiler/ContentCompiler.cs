// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler
{
    /// <summary>
    /// Provides methods for writing compiled binary format.
    /// </summary>
    public sealed class ContentCompiler
    {
        readonly Dictionary<Type, Type> typeWriterMap = new Dictionary<Type, Type>();

        /// <summary>
        /// Initializes a new instance of ContentCompiler.
        /// </summary>
        public ContentCompiler()
        {
            GetTypeWriters();
        }

        /// <summary>
        /// Iterates through all loaded assemblies and finds the content type writers.
        /// </summary>
        void GetTypeWriters()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                Type[] exportedTypes;
                try
                {
                    exportedTypes = assembly.GetTypes();
                }
                catch (Exception)
                {
                    continue;
                }

                var contentTypeWriterType = typeof(ContentTypeWriter<>);
                foreach (var type in exportedTypes)
                {
					if (type.IsAbstract)
                        continue;
                    if (Attribute.IsDefined(type, typeof(ContentTypeWriterAttribute)))
                    {
                        // Find the content type this writer implements
                        Type? baseType = type.BaseType;
                        while (baseType != null && (!baseType.IsGenericType || baseType.GetGenericTypeDefinition() != contentTypeWriterType))
                            baseType = baseType.BaseType;
                        if (baseType != null)
                            typeWriterMap.Add(baseType, type);
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves the worker writer for the specified type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The worker writer.</returns>
        /// <remarks>This should be called from the ContentTypeWriter.Initialize method.</remarks>
        public ContentTypeWriter GetTypeWriter(Type type)
        {
            ContentTypeWriter? result = null;
            var contentTypeWriterType = typeof(ContentTypeWriter<>).MakeGenericType(type);
            Type? typeWriterType;

            if (type == typeof(Array))
                result = new ArrayWriter<Array>();
            else if (typeWriterMap.TryGetValue(contentTypeWriterType, out typeWriterType))
                result = (ContentTypeWriter)(Activator.CreateInstance(typeWriterType)
                    ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Could not create content type writer '{0}'.", typeWriterType.FullName)));
            else if (type.IsArray)
            {
                var writerType = type.GetArrayRank() == 1 ? typeof(ArrayWriter<>) : typeof(MultiArrayWriter<>);
                var elementType = type.GetElementType()
                    ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Array type '{0}' is missing an element type.", type.FullName));

                result = (ContentTypeWriter)(Activator.CreateInstance(writerType.MakeGenericType(elementType))
                    ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Could not create content type writer '{0}'.", writerType.FullName)));
                typeWriterMap.Add(contentTypeWriterType, result.GetType());
            }
            else if (type.IsEnum)
            {
                result = (ContentTypeWriter)(Activator.CreateInstance(typeof(EnumWriter<>).MakeGenericType(type))
                    ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Could not create enum writer for '{0}'.", type.FullName)));
                typeWriterMap.Add(contentTypeWriterType, result.GetType());
            }
            else if (type.IsGenericType)
            {
                var inputTypeDef = type.GetGenericTypeDefinition();

                Type? chosen = null;
                foreach (var kvp in typeWriterMap)
                {
                    var args = kvp.Key.GetGenericArguments();

                    if (args.Length == 0)
                        continue;

                    if (!kvp.Value.IsGenericTypeDefinition)
                        continue;

                    if (!args[0].IsGenericType)
                        continue;

                    // Compare generic type definition
                    var keyTypeDef = args[0].GetGenericTypeDefinition();
                    if (inputTypeDef == keyTypeDef)
                    {
                        chosen = kvp.Value;
                        break;
                    }
                }

                try
                {
                    if (chosen == null)
                        result = (ContentTypeWriter)(Activator.CreateInstance(typeof(ReflectiveWriter<>).MakeGenericType(type))
                            ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Could not create reflective writer for '{0}'.", type.FullName)));
                    else
                    {
                        var concreteType = type.GetGenericArguments();
                        result = (ContentTypeWriter)(Activator.CreateInstance(chosen.MakeGenericType(concreteType))
                            ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Could not create generic writer '{0}'.", chosen.FullName)));
                    }

                    // save it for next time.
                    typeWriterMap.Add(contentTypeWriterType, result.GetType());
                }
                catch (Exception)
                {
                    throw new InvalidContentException(string.Format(CultureInfo.InvariantCulture, "Could not find ContentTypeWriter for type '{0}'", type.Name));
                }
            }
            else
            {
                result = (ContentTypeWriter)(Activator.CreateInstance(typeof(ReflectiveWriter<>).MakeGenericType(type))
                    ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Could not create reflective writer for '{0}'.", type.FullName)));
                typeWriterMap.Add(contentTypeWriterType, result.GetType());
            }


            var initMethod = result.GetType().GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Content type writer '{0}' is missing Initialize.", result.GetType().FullName));
            initMethod.Invoke(result, new object[] { this });

            return result;
        }

        /// <summary>
        /// Write the content to a XNB file.
        /// </summary>
        /// <param name="stream">The stream to write the XNB file to.</param>
        /// <param name="content">The content to write to the XNB file.</param>
        /// <param name="targetPlatform">The platform the XNB is intended for.</param>
        /// <param name="targetProfile">The graphics profile of the target.</param>
        /// <param name="compressContent">True if the content should be compressed.</param>
        /// <param name="rootDirectory">The root directory of the content.</param>
        /// <param name="referenceRelocationPath">The path of the XNB file, used to calculate relative paths for external references.</param>
        public void Compile(Stream stream, object content, TargetPlatform targetPlatform, GraphicsProfile targetProfile, bool compressContent, string rootDirectory, string referenceRelocationPath)
        {
            using (var writer = new ContentWriter(this, stream, targetPlatform, targetProfile, compressContent, rootDirectory, referenceRelocationPath))
            {
                writer.WriteObject(content);
                writer.FinalizeContent();
                writer.Flush();
            }
        }
    }
}
