namespace Bot.Builder.FileSystemStorage
{
    // NO Copyright (c) by aBaTaPbl4
    // Licensed under the NO License.

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Bot.Builder;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;


    /// <summary>
    /// A storage layer that uses an in-memory dictionary.
    /// </summary>
    public class FileSystemStorage : IStorage
    {
        private static readonly JsonSerializer StateJsonSerializer = new JsonSerializer() { TypeNameHandling = TypeNameHandling.All };            
        private readonly object _syncroot = new object();
        private int _eTag = 0;
        private DirectoryInfo _dir;


        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryStorage"/> class.
        /// </summary>
        /// <param name="locationPath">Data directory path</param>
        public FileSystemStorage(string dataDirectoryPath = "File_System_Storage")
        {
            _dir = new DirectoryInfo(dataDirectoryPath);
            if (!_dir.Exists)
            {
                _dir.Create();
            }               
        }

        /// <summary>
        /// Deletes storage items from storage.
        /// </summary>
        /// <param name="keys">keys of the <see cref="IStoreItem"/> objects to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <seealso cref="ReadAsync(string[], CancellationToken)"/>
        /// <seealso cref="WriteAsync(IDictionary{string, object}, CancellationToken)"/>
        public Task DeleteAsync(string[] keys, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(
                () =>
                {
                    lock (_syncroot)
                    {
                        foreach (string key in keys)
                        {
                            var file = GetFile(key);
                            if (file.Exists)
                                file.Delete();
                        }
                    }
                });
        }

        /// <summary>
        /// Reads storage items from storage.
        /// </summary>
        /// <param name="keys">keys of the <see cref="IStoreItem"/> objects to read.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the activities are successfully sent, the task result contains
        /// the items read, indexed by key.</remarks>
        /// <seealso cref="DeleteAsync(string[], CancellationToken)"/>
        /// <seealso cref="WriteAsync(IDictionary{string, object}, CancellationToken)"/>
        public Task<IDictionary<string, object>> ReadAsync(string[] keys, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew<IDictionary<string, object>>(
                () =>
                {
                    var storeItems = new Dictionary<string, object>(keys.Length);
                    lock (_syncroot)
                    {
                        foreach (string key in keys)
                        {
                            if (TryGetValue(key, out JObject state))
                            {
                                if (state != null)
                                {
                                    storeItems.Add(key, state.ToObject<object>(StateJsonSerializer));
                                }
                            }
                        }
                    }

                    return storeItems;
                });
        }


        /// <summary>
        /// Writes storage items to storage.
        /// </summary>
        /// <param name="changes">The items to write, indexed by key.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <seealso cref="DeleteAsync(string[], CancellationToken)"/>
        /// <seealso cref="ReadAsync(string[], CancellationToken)"/>
        public Task WriteAsync(IDictionary<string, object> changes, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(
                () =>
                {
                    lock (_syncroot)
                    {
                        foreach (var change in changes)
                        {
                            var newValue = change.Value;

                            var oldStateETag = default(string);

                            if (TryGetValue(change.Key, out var oldState))
                            {
                                if (oldState.TryGetValue("eTag", out var etag))
                                {
                                    oldStateETag = etag.Value<string>();
                                }
                            }

                            JObject newState = JObject.FromObject(newValue, StateJsonSerializer);

                            // Set ETag if applicable
                            if (newValue is IStoreItem newStoreItem)
                            {
                                if (oldStateETag != null
                                    &&
                                    newStoreItem.ETag != "*"
                                    &&
                                    newStoreItem.ETag != oldStateETag)
                                {
                                    throw new Exception($"Etag conflict.\r\n\r\nOriginal: {newStoreItem.ETag}\r\nCurrent: {oldStateETag}");
                                }

                                newState["eTag"] = (_eTag++).ToString();
                            }

                            SaveValue(change.Key, newState);
                        }
                    }
                });
        }


        private void SaveValue(string key, JObject jObject)
        {                
            using (FileStream strm = GetOrCreateFile(key))
            using (var sw = new StreamWriter(strm))
            using (var jsonWriter = new JsonTextWriter(sw))
            {
                strm.SetLength(0);
                jObject.WriteTo(jsonWriter);
            }
        }


        private bool TryGetValue(string key, out JObject jObject)
        {
            FileInfo fileInfo = GetFile(key);
            if (!fileInfo.Exists)
            {
                jObject = null;
                return false;
            }
            
            using (FileStream strm = fileInfo.OpenRead())
            using (var sr = new StreamReader(strm))
            using (var jsonReader = new JsonTextReader(sr))
            {
                jObject = (JObject)JToken.ReadFrom(jsonReader);
            }
            return true;
        }

        private FileStream GetOrCreateFile(string key)
        {
            FileInfo fileInfo = GetFile(key);
            if (fileInfo.Exists)
            {
                return fileInfo.OpenWrite();
            }
            else
            {
                return fileInfo.Create();
            }
        }


        private FileInfo GetFile(string key)
        {
            string fileName = GetFileNameByKey(key);
            string filePath = Path.Combine(_dir.FullName, fileName);
            return new FileInfo(filePath);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetFileNameByKey(string key)
        {
            string result = key;
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char invalidChar in invalidChars)
            {
                result = result.Replace(invalidChar, '_');
            }
            return result;
        }
    }    
}
