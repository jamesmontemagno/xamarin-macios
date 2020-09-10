using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using Mono.Linker;
using Mono.Linker.Steps;

using Xamarin.Bundler;
using Xamarin.Linker;

namespace Xamarin {

	public class SetupStep : ConfigurationAwareStep {

		List<IStep> _steps;
		public List<IStep> Steps {
			get {
				if (_steps == null) {
					var pipeline = typeof (LinkContext).GetProperty ("Pipeline").GetGetMethod ().Invoke (Context, null);
					_steps = (List<IStep>) pipeline.GetType ().GetField ("_steps", BindingFlags.Instance | BindingFlags.NonPublic).GetValue (pipeline);
				}
				return _steps;
			}
		}

		protected override void Process ()
		{
			// Don't use --custom-step to load each step, because this assembly
			// is loaded into the current process once per --custom-step,
			// which makes it very difficult to share state between steps.

			// Load the list of assemblies loaded by the linker.
			// This would not be needed of LinkContext.GetAssemblies () was exposed to us.
			InsertAfter (new CollectAssembliesStep (), "LoadReferencesStep");

			var prelink_substeps = new DotNetSubStepDispatcher ();
			InsertAfter (prelink_substeps, "RemoveSecurityStep");

			if (Configuration.LinkMode != LinkMode.None) {
				// We need to run the ApplyPreserveAttribute step even we're only linking sdk assemblies, because even
				// though we know that sdk assemblies will never have Preserve attributes, user assemblies may have
				// [assembly: LinkSafe] attributes, which means we treat them as sdk assemblies and those may have
				// Preserve attributes.
				prelink_substeps.Add (new ApplyPreserveAttribute ());
				prelink_substeps.Add (new OptimizeGeneratedCodeSubStep ());
				prelink_substeps.Add (new MarkNSObjects ());
				prelink_substeps.Add (new PreserveSmartEnumConversionsSubStep ());
			}

			Steps.Add (new LoadNonSkippedAssembliesStep ());
			Steps.Add (new ExtractBindingLibrariesStep ());
			Steps.Add (new GenerateMainStep ());
			Steps.Add (new GatherFrameworksStep ());

			Configuration.Write ();

			if (Configuration.Verbosity > 0) {
				Console.WriteLine ();
				Console.WriteLine ("Pipeline Steps:");
				foreach (var step in Steps) {
					Console.WriteLine ($"    {step}");
				}
			}

			ErrorHelper.Platform = Configuration.Platform;
			Directory.CreateDirectory (Configuration.ItemsDirectory);
			Directory.CreateDirectory (Configuration.CacheDirectory);
		}
	}
}
