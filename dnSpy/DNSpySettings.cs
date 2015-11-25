// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace ICSharpCode.ILSpy {
	/// <summary>
	/// Manages dnSpy settings.
	/// </summary>
	public class DNSpySettings {
		readonly XElement root;

		DNSpySettings() {
			this.root = new XElement("dnSpy");
		}

		DNSpySettings(XElement root) {
			this.root = root;
		}

		public XElement this[XName section] {
			get {
				return root.Element(section) ?? new XElement(section);
			}
		}

		public XElement GetElement(XName section) {
			return root.Element(section);
		}

		/// <summary>
		/// Loads the settings file from disk.
		/// </summary>
		/// <returns>
		/// An instance used to access the loaded settings.
		/// </returns>
		public static DNSpySettings Load() {
			using (new MutexProtector(ConfigFileMutex)) {
				try {
					XDocument doc = LoadWithoutCheckingCharacters(GetConfigFile());
					return new DNSpySettings(doc.Root);
				}
				catch (IOException) {
					return new DNSpySettings();
				}
				catch (XmlException) {
					return new DNSpySettings();
				}
			}
		}

		static XDocument LoadWithoutCheckingCharacters(string fileName) {
			return XDocument.Load(fileName, LoadOptions.None);
		}

		/// <summary>
		/// Saves a setting section.
		/// </summary>
		public static void SaveSettings(XElement section) {
			Update(
				delegate (XElement root) {
					XElement existingElement = root.Element(section.Name);
					if (existingElement != null)
						existingElement.ReplaceWith(section);
					else
						root.Add(section);
				});
		}

		/// <summary>
		/// Updates the saved settings.
		/// We always reload the file on updates to ensure we aren't overwriting unrelated changes performed
		/// by another dnSpy instance.
		/// </summary>
		public static void Update(Action<XElement> action) {
			using (new MutexProtector(ConfigFileMutex)) {
				string config = GetConfigFile();
				XDocument doc;
				try {
					doc = LoadWithoutCheckingCharacters(config);
				}
				catch (IOException) {
					// ensure the directory exists
					Directory.CreateDirectory(Path.GetDirectoryName(config));
					doc = new XDocument(new XElement("dnSpy"));
				}
				catch (XmlException) {
					doc = new XDocument(new XElement("dnSpy"));
				}
				doc.Root.SetAttributeValue("version", typeof(MainWindow).Assembly.GetName().Version.ToString());
				action(doc.Root);
				doc.Save(config, SaveOptions.None);
			}
		}

		static string GetConfigFile() {
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "dnSpy", "dnSpy.xml");
		}

		const string ConfigFileMutex = "D37A50B7-7071-4727-886E-A69D4F24AFA4";

		/// <summary>
		/// Helper class for serializing access to the config file when multiple dnSpy instances are running.
		/// </summary>
		sealed class MutexProtector : IDisposable {
			readonly Mutex mutex;

			public MutexProtector(string name) {
				bool createdNew;
				this.mutex = new Mutex(true, name, out createdNew);
				if (!createdNew) {
					try {
						mutex.WaitOne();
					}
					catch (AbandonedMutexException) {
					}
				}
			}

			public void Dispose() {
				mutex.ReleaseMutex();
				mutex.Dispose();
			}
		}
	}
}
