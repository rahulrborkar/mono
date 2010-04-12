//
// Copyright (C) 2010 Novell Inc. http://novell.com
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Markup;
using System.Xaml.Schema;

namespace System.Xaml
{
	// This type caches assembly attribute search results. To do this,
	// it registers AssemblyLoaded event on CurrentDomain when it should
	// reflect dynamic in-scope asemblies.
	// It should be released at finalizer.
	public class XamlSchemaContext
	{
		public XamlSchemaContext (IEnumerable<Assembly> referenceAssemblies)
			: this (referenceAssemblies, null)
		{
		}

		public XamlSchemaContext (XamlSchemaContextSettings settings)
			: this (null, settings)
		{
		}

		public XamlSchemaContext (IEnumerable<Assembly> referenceAssemblies, XamlSchemaContextSettings settings)
		{
			if (referenceAssemblies != null)
				reference_assemblies = new List<Assembly> (referenceAssemblies);
			else
				AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoaded;

			if (settings == null)
				return;

			FullyQualifyAssemblyNamesInClrNamespaces = settings.FullyQualifyAssemblyNamesInClrNamespaces;
			SupportMarkupExtensionsWithDuplicateArity = settings.SupportMarkupExtensionsWithDuplicateArity;
		}

		~XamlSchemaContext ()
		{
			if (reference_assemblies == null)
				AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoaded;
		}

		IList<Assembly> reference_assemblies;

		// assembly attribute caches
		List<string> xaml_nss;
		Dictionary<string,string> prefixes;
		Dictionary<string,string> compat_nss;
		Dictionary<string,List<XamlType>> all_xaml_types;
		XamlType [] empty_xaml_types = new XamlType [0];

		public bool FullyQualifyAssemblyNamesInClrNamespaces { get; private set; }

		public IList<Assembly> ReferenceAssemblies {
			get { return reference_assemblies; }
		}

		IEnumerable<Assembly> AssembliesInScope {
			get { return reference_assemblies ?? AppDomain.CurrentDomain.GetAssemblies (); }
		}

		public bool SupportMarkupExtensionsWithDuplicateArity { get; private set; }

		public virtual IEnumerable<string> GetAllXamlNamespaces ()
		{
			if (xaml_nss == null) {
				xaml_nss = new List<string> ();
				foreach (var ass in AssembliesInScope)
					FillXamlNamespaces (ass);
			}
			return xaml_nss;
		}

		public virtual ICollection<XamlType> GetAllXamlTypes (string xamlNamespace)
		{
			if (xamlNamespace == null)
				throw new ArgumentNullException ("xamlNamespace");
			if (all_xaml_types == null) {
				all_xaml_types = new Dictionary<string,List<XamlType>> ();
				foreach (var ass in AssembliesInScope)
					FillAllXamlTypes (ass);
			}

			List<XamlType> l;
			if (all_xaml_types.TryGetValue (xamlNamespace, out l))
				return l;
			else
				return empty_xaml_types;
		}

		public virtual string GetPreferredPrefix (string xmlns)
		{
			if (xmlns == null)
				throw new ArgumentNullException ("xmlns");
			if (prefixes == null) {
				prefixes = new Dictionary<string,string> ();
				foreach (var ass in AssembliesInScope)
					FillPrefixes (ass);
			}
			string ret;
			return prefixes.TryGetValue (xmlns, out ret) ? ret : "p"; // default
		}

		protected internal XamlValueConverter<TConverterBase> GetValueConverter<TConverterBase> (Type converterType, XamlType targetType)
			where TConverterBase : class
		{
			return new XamlValueConverter<TConverterBase> (converterType, targetType);
		}
		
		public virtual XamlDirective GetXamlDirective (string xamlNamespace, string name)
		{
			throw new NotImplementedException ();
		}
		
		Dictionary<Type,XamlType> xaml_types = new Dictionary<Type,XamlType> ();
		
		public virtual XamlType GetXamlType (Type type)
		{
			XamlType t;
			if (!xaml_types.TryGetValue (type, out t)) {
				t = new XamlType (type, this);
				xaml_types.Add (type, t);
			}
			return t;
		}
		
		public XamlType GetXamlType (XamlTypeName xamlTypeName)
		{
			throw new NotImplementedException ();
		}
		
		protected internal virtual XamlType GetXamlType (string xamlNamespace, string name, params XamlType[] typeArguments)
		{
			throw new NotImplementedException ();
		}

		protected internal virtual Assembly OnAssemblyResolve (string assemblyName)
		{
			return null;
		}

		public virtual bool TryGetCompatibleXamlNamespace (string xamlNamespace, out string compatibleNamespace)
		{
			if (xamlNamespace == null)
				throw new ArgumentNullException ("xamlNamespace");
			if (compat_nss == null) {
				compat_nss = new Dictionary<string,string> ();
				foreach (var ass in AssembliesInScope)
					FillCompatibilities (ass);
			}
			return compat_nss.TryGetValue (xamlNamespace, out compatibleNamespace);
		}

		void OnAssemblyLoaded (object o, AssemblyLoadEventArgs e)
		{
			if (reference_assemblies != null)
				return; // do nothing

			if (xaml_nss != null)
				FillXamlNamespaces (e.LoadedAssembly);
			if (prefixes != null)
				FillPrefixes (e.LoadedAssembly);
			if (compat_nss != null)
				FillCompatibilities (e.LoadedAssembly);
			if (all_xaml_types != null)
				FillAllXamlTypes (e.LoadedAssembly);
		}
		
		// cache updater methods
		void FillXamlNamespaces (Assembly ass)
		{
			foreach (XmlnsDefinitionAttribute xda in ass.GetCustomAttributes (typeof (XmlnsDefinitionAttribute), false))
				xaml_nss.Add (xda.XmlNamespace);
		}
		
		void FillPrefixes (Assembly ass)
		{
			foreach (XmlnsPrefixAttribute xpa in ass.GetCustomAttributes (typeof (XmlnsPrefixAttribute), false))
				prefixes.Add (xpa.XmlNamespace, xpa.Prefix);
		}
		
		void FillCompatibilities (Assembly ass)
		{
			foreach (XmlnsCompatibleWithAttribute xca in ass.GetCustomAttributes (typeof (XmlnsCompatibleWithAttribute), false))
				compat_nss.Add (xca.OldNamespace, xca.NewNamespace);
		}

		void FillAllXamlTypes (Assembly ass)
		{
			foreach (XmlnsDefinitionAttribute xda in ass.GetCustomAttributes (typeof (XmlnsDefinitionAttribute), false)) {
				var l = new List<XamlType> ();
				all_xaml_types.Add (xda.XmlNamespace, l);
				foreach (var t in ass.GetTypes ())
					if (t.Namespace == xda.ClrNamespace)
						l.Add (GetXamlType (t));
			}
		}
	}
}
