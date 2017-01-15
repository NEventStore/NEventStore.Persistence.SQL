﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace NEventStore.Persistence.Sql.SqlDialects {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class FirebirdSqlStatements {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal FirebirdSqlStatements() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("NEventStore.Persistence.Sql.SqlDialects.FirebirdSqlStatements", typeof(FirebirdSqlStatements).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to INSERT
        ///INTO Snapshots
        /// ( BucketId, StreamId, StreamRevision, Payload )
        ///SELECT @BucketId, @StreamId, @StreamRevision, @Payload FROM rdb$database
        ///WHERE EXISTS ( SELECT * FROM Commits WHERE BucketId = @BucketId AND StreamId = @StreamId AND (StreamRevision - Items) &lt;= @StreamRevision )
        ///AND NOT EXISTS ( SELECT * FROM Snapshots WHERE BucketId = @BucketId AND StreamId = @StreamId AND StreamRevision = @StreamRevision );.
        /// </summary>
        internal static string AppendSnapshotToCommit {
            get {
                return ResourceManager.GetString("AppendSnapshotToCommit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SELECT FIRST @Limit SKIP @Skip BucketId, StreamId, StreamIdOriginal, StreamRevision, CommitId, CommitSequence, CommitStamp, CheckpointNumber, Headers, Payload
        ///  FROM Commits
        /// WHERE BucketId = @BucketId 
        ///   AND CheckpointNumber &gt; @CheckpointNumber
        /// ORDER BY CheckpointNumber;.
        /// </summary>
        internal static string GetCommitsFromBucketAndCheckpoint {
            get {
                return ResourceManager.GetString("GetCommitsFromBucketAndCheckpoint", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SELECT FIRST @Limit SKIP @Skip BucketId, StreamId, StreamIdOriginal, StreamRevision, CommitId, CommitSequence, CommitStamp, CheckpointNumber, Headers, Payload
        ///FROM Commits
        ///WHERE  CheckpointNumber &gt; @CheckpointNumber
        ///ORDER BY CheckpointNumber;.
        /// </summary>
        internal static string GetCommitsFromCheckpoint {
            get {
                return ResourceManager.GetString("GetCommitsFromCheckpoint", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SELECT FIRST @Limit SKIP @Skip BucketId, StreamId, StreamIdOriginal, StreamRevision, CommitId, CommitSequence, CommitStamp, CheckpointNumber, Headers, Payload
        ///  FROM Commits
        /// WHERE BucketId = @BucketId AND CommitStamp &gt;= @CommitStamp
        /// ORDER BY CommitStamp, StreamId, CommitSequence;.
        /// </summary>
        internal static string GetCommitsFromInstant {
            get {
                return ResourceManager.GetString("GetCommitsFromInstant", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SELECT FIRST @Limit BucketId, StreamId, StreamIdOriginal, StreamRevision, CommitId, CommitSequence, CommitStamp,  CheckpointNumber, Headers, Payload
        ///  FROM Commits
        /// WHERE BucketId = @BucketId
        ///   AND StreamId = @StreamId
        ///   AND StreamRevision &gt;= @StreamRevision
        ///   AND (StreamRevision - Items) &lt; @MaxStreamRevision
        ///   AND CommitSequence &gt; @CommitSequence
        /// ORDER BY CommitSequence;.
        /// </summary>
        internal static string GetCommitsFromStartingRevision {
            get {
                return ResourceManager.GetString("GetCommitsFromStartingRevision", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SELECT FIRST @Limit SKIP @Skip BucketId, StreamId, StreamIdOriginal, StreamRevision, CommitId, CommitSequence, CommitStamp, CheckpointNumber, Headers, Payload
        ///  FROM Commits
        /// WHERE BucketId = @BucketId
        ///   AND CommitStamp &gt;= @CommitStampStart
        ///   AND CommitStamp &lt; @CommitStampEnd
        /// ORDER BY CommitStamp, StreamId, CommitSequence;.
        /// </summary>
        internal static string GetCommitsFromToInstant {
            get {
                return ResourceManager.GetString("GetCommitsFromToInstant", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SELECT FIRST 1 *
        ///  FROM Snapshots
        /// WHERE BucketId = @BucketId
        ///   AND StreamId = @StreamId
        ///   AND StreamRevision &lt;= @StreamRevision
        /// ORDER BY StreamRevision DESC;.
        /// </summary>
        internal static string GetSnapshot {
            get {
                return ResourceManager.GetString("GetSnapshot", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SELECT FIRST @Limit C.BucketId, C.StreamId, C.StreamIdOriginal, MAX(C.StreamRevision) AS StreamRevision, MAX(COALESCE(S.StreamRevision, 0)) AS SnapshotRevision
        ///  FROM Commits AS C
        /// LEFT OUTER JOIN Snapshots AS S
        ///	ON C.BucketId = @BucketId
        ///   AND C.StreamId = S.StreamId
        ///   AND C.StreamRevision &gt;= S.StreamRevision
        /// GROUP BY C.StreamId, C.BucketId, C.StreamIdOriginal
        ///HAVING MAX(C.StreamRevision) &gt;= MAX(COALESCE(S.StreamRevision, 0)) + @Threshold
        /// ORDER BY C.StreamId;.
        /// </summary>
        internal static string GetStreamsRequiringSnapshots {
            get {
                return ResourceManager.GetString("GetStreamsRequiringSnapshots", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SELECT FIRST @Limit SKIP @Skip BucketId, StreamId, StreamIdOriginal, StreamRevision, CommitId, CommitSequence, CommitStamp, CheckpointNumber, Headers, Payload
        ///  FROM Commits
        /// WHERE Dispatched = 0
        /// ORDER BY CheckpointNumber;.
        /// </summary>
        internal static string GetUndispatchedCommits {
            get {
                return ResourceManager.GetString("GetUndispatchedCommits", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to CREATE TABLE Commits
        ///	(
        ///	   BucketId varchar(40) NOT NULL,
        ///	   StreamId char(40) NOT NULL,
        ///	   StreamIdOriginal varchar (1000) NOT NULL,
        ///	   StreamRevision int NOT NULL CHECK (StreamRevision &gt; 0),
        ///	   Items int NOT NULL CHECK (Items &gt; 0),
        ///	   CommitId char(16) character set octets NOT NULL,
        ///	   CommitSequence int NOT NULL CHECK (CommitSequence &gt; 0),
        ///	   CommitStamp timestamp NOT NULL,
        ///	   CheckpointNumber int PRIMARY KEY,
        ///	   Dispatched char (1) DEFAULT &apos;0&apos; NOT NULL,
        ///	   Headers blob,
        ///	   Paylo [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string InitializeStorage {
            get {
                return ResourceManager.GetString("InitializeStorage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to INSERT
        ///  INTO Commits
        ///	 ( BucketId, StreamId, StreamIdOriginal, CommitId, CommitSequence, StreamRevision, Items, CommitStamp, Headers, Payload )
        ///VALUES (@BucketId, @StreamId, @StreamIdOriginal, @CommitId, @CommitSequence, @StreamRevision, @Items, @CommitStamp, @Headers, @Payload)
        ///RETURNING CheckpointNumber;.
        /// </summary>
        internal static string PersistCommits {
            get {
                return ResourceManager.GetString("PersistCommits", resourceCulture);
            }
        }
    }
}
