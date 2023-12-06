# Storage Facade Library

This library is intended for providng a standard interface to storage.  The source can be found here: https://github.com/Beztek-Software-Solutions/storage-facade

# Overview

It is intended to be portable to multiple kinds of storage, such as Azure Blob Storage, Local files, SMB file shares, etc. It can access SMB shares from Linux-based servers

## Poor Man's DFS (Domain File Share) implementation

The SMB Networking provider also has a "poor-man's" way of accessing DFS shares. Although the underlying SMBLibrary does not provide  DFS Support, this object can configure an SMB Share with an optional physical server that maps the DFS name. This mapping is handled transparently by the library. For example, if the DFS name is \\\\\<server1>\\\<path1>\\\<path2> which is the DFS share for \\\\\<physical server 1>\\\<path2>, if the DFS name \\\\\<server1>\\\<path1> is mapped to the physical server \\\\\<physical server 1> (where \<path2> is the share name), this library will correctly access all the DFS files transparently. So while this library cannot automatically resolve DFS names, this provides a work-around, as long as the destination physical server does not change while this instance is up.

## Combine multiple shares for a seamless experience

There is also a Combo StorageFacade class which combines multple authenticated SMB Networking share providers along with the local file system provider, to provide a unified experience of seamlessly accessing SMB network shares, and local files using the same single facade, as if one were a logged-in windows user in the network.