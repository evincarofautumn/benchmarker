﻿using System;
using Parse;
using Nito.AsyncEx;
using System.Linq;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Benchmarker.Common.Models;

namespace Benchmarker.Common
{
	public class ParseInterface
	{
		private const string parseCredentialsFilename = "parse.pw";

		static ParseACL defaultACL;

		private static void WaitForConfirmation (string key)
		{
			Console.WriteLine ("Log in on browser for access, confirmation key {0}", key);
			ParseQuery<ParseObject> query = ParseObject.GetQuery ("CredentialsResponse").WhereEqualTo ("key", key);
			while (true) {
				var task = query.FirstOrDefaultAsync ();
				//Console.WriteLine ("FindOrDefaultAsync CredentialsResponse");
				task.Wait ();

				var result = task.Result;
				if (result != null) {
					// FIXME: check that it's successful
					break;
				}
				Thread.Sleep (1000);
			}
			Console.WriteLine ("Login successful");
		}

		private static string GetResponse (string url, string parameters)
		{
			WebRequest webRequest = WebRequest.Create(url);
			byte[] dataStream = Encoding.UTF8.GetBytes (parameters);
			webRequest.Method = "POST";
			webRequest.ContentType = "application/x-www-form-urlencoded";
			webRequest.ContentLength = dataStream.Length;
			using (Stream requestStream = webRequest.GetRequestStream ()) {
				requestStream.Write (dataStream, 0, dataStream.Length);
			}
			WebResponse webResponse = webRequest.GetResponse ();
			string response = new StreamReader(webResponse.GetResponseStream ()).ReadToEnd ();
			return response;
		}

		static void InitializeParse (string applicationId, string dotNetKey)
		{
			ParseClient.Initialize (applicationId, dotNetKey);
			if (ParseUser.CurrentUser != null) {
				try {
					ParseUser.LogOut ();
				} catch (Exception) {
				}
			}
		}

		public static bool Initialize ()
		{
			try {
				var credentials = Credentials.LoadFromFile (parseCredentialsFilename);

				if (credentials == null) {
					// Accredit
					InitializeParse ("RAePvLdkN2IHQNZRckrVXzeshpFZTgYif8qu5zuh", "giWKLzMOZa2nrgBjC9YPRF238CTVTpNsMlsIJkr3");

					string key = Guid.NewGuid ().ToString ();
					string secret = Guid.NewGuid ().ToString ();

					/* Get github OAuth authentication link */
					string oauthLink = GetResponse ("https://accredit.parseapp.com/requestCredentials", string.Format("service=benchmarker&key={0}&secret={1}", key, secret));

					/* Log in github OAuth */
					System.Diagnostics.Process.Start (oauthLink);

					/* Wait for login confirmation */
					WaitForConfirmation (key);

					/* Request the password */
					credentials = Credentials.LoadFromString (GetResponse ("https://accredit.parseapp.com/getCredentials", string.Format ("key={0}&secret={1}", key, secret)));

					/* Cache it in the current folder for future use */
					Credentials.WriteToFile (credentials, parseCredentialsFilename);
				}

				// Xamarin Performance
				InitializeParse ("7khPUBga9c7L1YryD1se1bp6VRzKKJESc0baS9ES", "FwqUX9gNQP5HmP16xDcZRoh0jJRCDvdoDpv8L87p");

				var user = AsyncContext.Run (() => ParseInterface.RunWithRetry (() => ParseUser.LogInAsync (credentials.Username, credentials.Password)));
				//Console.WriteLine ("LogInAsync");

				//Console.WriteLine ("User authenticated: " + user.IsAuthenticated);

				var acl = new ParseACL (user);
				acl.PublicReadAccess = true;
				acl.PublicWriteAccess = false;

				defaultACL = acl;
			} catch (Exception e) {
				Console.WriteLine ("Exception when initializing Parse API: {0}", e);
				return false;
			}
			return true;
		}

		public static ParseObject NewParseObject (string className)
		{
			if (defaultACL == null)
				throw new Exception ("ParseInterface must be initialized before ParseObjects can be created.");
			var obj = new ParseObject (className);
			obj.ACL = defaultACL;
			return obj;
		}

		public static double NumberAsDouble (object o)
		{
			if (o.GetType () == typeof (long))
				return (double)(long)o;
			if (o.GetType () == typeof (double))
				return (double)o;
			throw new Exception ("Number is neither double nor long.");
		}

		public async static Task RunWithRetry (Func<Task> run, Type acceptedException = null, int numTries = 3) {
			for (var i = 0; i < numTries - 1; ++i) {
				try {
					await run ();
					return;
				} catch (Exception exc) {
					if (acceptedException != null && acceptedException.IsAssignableFrom (exc.GetType ()))
						throw exc;
					var seconds = (i == 0) ? 10 : 60 * i;
					Console.Error.WriteLine ("Exception when running task - sleeping {0} seconds and retrying: {1}", seconds, exc);
					await Task.Delay (seconds * 1000);
				}
			}
			await run ();
		}

		public async static Task<T> RunWithRetry<T> (Func<Task<T>> run, Type acceptedException = null, int numTries = 3) {
			for (var i = 0; i < numTries - 1; ++i) {
				try {
					return await run ();
				} catch (Exception exc) {
					if (acceptedException != null && acceptedException.IsAssignableFrom (exc.GetType ()))
						throw exc;
					var seconds = (i == 0) ? 10 : 60 * i;
					Console.Error.WriteLine ("Exception when running task - sleeping {0} seconds and retrying: {1}", seconds, exc);
					await Task.Delay (seconds * 1000);
				}
			}
			return await run ();
		}

		public static async Task<IEnumerable<T>> PageQueryWithRetry<T> (Func<ParseQuery<T>> makeQuery) where T : ParseObject
		{
			List<T> results = new List<T> ();
			var limit = 100;
			for (var skip = 0;; skip += limit) {
				var page = await RunWithRetry (() => {
					var query = makeQuery ().Limit (limit).Skip (skip);
					//Console.WriteLine ("skipping {0}", skip);
					return query.FindAsync ();
				});
				results.AddRange (page);
				if (page.Count () < limit)
					break;
			}
			return results;
		}
	}
}
