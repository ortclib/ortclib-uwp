﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

namespace org
{
    namespace ortc
    {
        namespace adapter
        {

            class SDPConvertor
            {
                internal static void ParseSdp(string sdp, RTCSdpType? type, RTCPeerConnection peerConnection)
                {
                    string sessionId = null;
                    string sessionVersion = null;
                    string streamId = null;
                    bool audioSupported = false;
                    bool videoSupported = false;
                    string ice_ufrag = null;
                    string ice_pwd = null;
                    string fingerprint = null;
                    List<Dictionary<string, string>> extmaps = new List<Dictionary<string, string>>();
                    List<string> codecs = new List<string>();

                    //string[] parts = sdp.Split("m=".ToCharArray());
                    string[] parts = sdp.Split(new string[] {"m="}, StringSplitOptions.None);
                    if (parts.Length < 2)
                        throw new Exception("invalid SDP");

                    bool parsedSuccessfully = parseCommonSdp(parts[0], out sessionId, out sessionVersion, out streamId, out audioSupported,
                        out videoSupported);

                    if (!parsedSuccessfully)
                        throw new Exception("invalid SDP");

                    for (int i = 1; i < parts.Length; i++)
                    {
                        parseMediaSdp(parts[i], out ice_ufrag, out ice_pwd, out fingerprint, extmaps, codecs);
                    }

                    return;
                    
                }

                internal static bool parseCommonSdp(string sdp, out string sessionId, out string sessionVersion,
                    out string streamId, out bool audioSupported, out bool videoSupported)
                {
                    bool valid = true;
                    sessionId = null;
                    sessionVersion = null;
                    streamId = null;
                    audioSupported = false;
                    videoSupported = false;

                    try
                    {
                        TextReader reader = new StringReader(sdp);

                        string line = reader.ReadLine();
                        while (line != null)
                        {
                            if (line.Length == 0)
                            {
                                line = reader.ReadLine();
                                continue;
                            }
                            string value = line.Substring(2);
                            string[] parts = null;
                            int sep = -1;
                            try
                            {
                                switch (line[0])
                                {
                                    case 'v':
                                        int version = 0;
                                        if (!int.TryParse(value, out version))
                                        {
                                            throw new Exception(string.Format("Invalid Line {0}", line));
                                        }
                                        if (version != 0)
                                        {
                                            throw new Exception(string.Format("Not supported version"));
                                        }
                                        break;
                                    case 'o':
                                        parts = value.Split(' ');
                                        if (parts.Length != 6)
                                        {
                                            throw new Exception(string.Format("Invalid Line {0}", line));
                                        }
                                        sessionId = parts[1];
                                        sessionVersion = parts[2];
                                        break;
                                    case 's':
                                        break;
                                    case 't':
                                        break;
                                    case 'a':
                                        sep = value.IndexOf(':');
                                        string attrName;
                                        string attrValue;

                                        if (sep == -1)
                                            break;

                                        attrName = value.Substring(0, sep);
                                        attrValue = value.Substring(sep + 1);

                                        if (attrName.Equals("group"))
                                        {
                                            audioSupported = attrValue.Contains("audio");
                                            videoSupported = attrValue.Contains("video");
                                        }
                                        else if (attrName.Equals("msid-semantic"))
                                        {
                                            streamId = attrValue;
                                        }
                                        break;
                                }
                            }
                            catch
                            {
                                throw new Exception(string.Format("Invalid Line {0}", line));
                            }
                            line = reader.ReadLine();
                        }
                    }
                    catch
                    {
                        valid = false;
                    }
                    return valid;
                }

                internal static bool parseMediaSdp(string sdp, out string ice_ufrag, out string ice_pwd,
                    out string fingerprint, List<Dictionary<string, string>> extmaps, List<string> codecs)
                {
                    bool valid = true;
                    ice_ufrag = null;
                    ice_pwd = null;

                    RTCIceParameters iceParameters = new RTCIceParameters();

                    Dictionary<string,RTCRtpCodecCapability> codecsDictionary = new Dictionary<string, RTCRtpCodecCapability>();
                    List<RTCRtpHeaderExtension> headerExtensions = new List<RTCRtpHeaderExtension>();

                    RTCDtlsParameters dtlsParameters = new RTCDtlsParameters();
                    dtlsParameters.Fingerprints = new List<RTCDtlsFingerprint>();
                    fingerprint = null;
                    TextReader reader = new StringReader(sdp);

                    IList<UInt32> streamSourceIds = new List<uint>();
                    string streamName = null;
                    string mediaTrackLabel = null;
                    string streamId = null;

                    string line = reader.ReadLine();
                    while (line != null)
                    {
                        if (line.Length == 0)
                        {
                            line = reader.ReadLine();
                            continue;
                        }
                        
                        try
                        {
                            if (line.StartsWith("audio") || line.StartsWith("video"))
                            {
                                string[] list = line.Split(' ');
                                var codecsList = new List<string>(list);
                                codecsList.RemoveRange(0,3);
                                if (codecsList.Count > 0)
                                {
                                    foreach (var codecId in codecsList)
                                    {
                                        RTCRtpCodecCapability codecCapability = new RTCRtpCodecCapability();
                                        codecCapability.PreferredPayloadType = byte.Parse(codecId);
                                        codecsDictionary.Add(codecId,codecCapability);
                                    }
                                    codecs.AddRange(codecsList.Where(codecId => codecId.Length > 0));
                                }
                            }
                            else
                            {
                                string value = line.Substring(2);
                                string[] parts = null;
                                int sep = -1;

                                switch (line[0])
                                {
                                    case 'c':
                                        parts = value.Split(' ');
                                        if (parts.Length != 3)
                                        {
                                            throw new Exception(string.Format("Invalid Line {0}", line));
                                        }
                                        string networkType = parts[0];
                                        string addressType = parts[1];
                                        parts = parts[2].Split('/');
                                        if (parts.Length > 3)
                                        {
                                            throw new Exception(string.Format("Invalid Line {0}", line));
                                        }
                                        //TODO: Check if this is required
                                        break;
                                    case 'a':
                                        sep = value.IndexOf(':');
                                        string attrName;
                                        string attrValue;

                                        if (sep == -1)
                                            break;

                                        attrName = value.Substring(0, sep);
                                        attrValue = value.Substring(sep + 1);

                                        if (attrName.Equals("candidate"))
                                        {
                                            //org.ortc.RTCIceCandidate iceCandidate;
                                            //iceCandidate = iceCandidateFromSdp(attrValue);
                                        }
                                        else if (attrName.Equals("rtcp"))
                                        {

                                        }
                                        else if (attrName.Equals("ice-ufrag"))
                                        {
                                            iceParameters.UsernameFragment = attrValue;
                                        }
                                        else if (attrName.Equals("ice-pwd"))
                                        {
                                            iceParameters.Password = attrValue;
                                        }
                                        else if (attrName.Equals("fingerprint"))
                                        {
                                            sep = attrValue.IndexOf(' ');

                                            if (sep == -1)
                                                break;

                                            RTCDtlsFingerprint dtlsFingerprint = new RTCDtlsFingerprint();
                                            dtlsFingerprint.Algorithm = attrValue.Substring(0, sep);
                                            dtlsFingerprint.Value = attrValue.Substring(sep + 1);
                                            dtlsParameters.Fingerprints.Add(dtlsFingerprint);
                                        }
                                        else if (attrName.Equals("setup"))
                                        {
                                            //TODO Check when should be set client or server
                                            //dtlsParameters.Role = attrValue.Equals("actpass") ? RTCDtlsRole.Auto : RTCDtlsRole.Auto;
                                            dtlsParameters.Role = RTCDtlsRole.Auto;
                                        }
                                        else if (attrName.Equals("mid"))
                                        {

                                        }
                                        else if (attrName.Equals("extmap"))
                                        {
                                            sep = attrValue.IndexOf(' ');

                                            if (sep == -1)
                                                break;
                                            RTCRtpHeaderExtension headerExtension = new RTCRtpHeaderExtension();
                                            //string str = attrValue.Substring(0, sep);
                                            headerExtension.PreferredId = ushort.Parse(attrValue.Substring(0, sep));
                                            headerExtension.Uri = attrValue.Substring(sep + 1);
                                            headerExtensions.Add(headerExtension);
                                        }
                                        else if (attrName.Equals("sendrecv"))
                                        {

                                        }
                                        else if (attrName.Equals("rtcp-mux"))
                                        {

                                        }
                                        else if (attrName.Equals("rtpmap"))
                                        {
                                            //a = rtpmap:100 VP8 / 90000
                                            sep = attrValue.IndexOf(' ');

                                            if (sep == -1)
                                                break;

                                            string codecId = attrValue.Substring(0, sep);
                                            if (null == codecId) break;

                                            RTCRtpCodecCapability capability = null;
                                            if (codecsDictionary.TryGetValue(codecId, out capability))
                                            {
                                                string temp = attrValue.Substring(sep + 1);
                                                if (null == temp)
                                                    break;

                                                string[] codecInfoStrings = temp.Split('/');

                                                if (codecInfoStrings.Length > 1)
                                                {
                                                    capability.Name = codecInfoStrings[0];
                                                    capability.ClockRate = uint.Parse(codecInfoStrings[1]);
                                                    if (codecInfoStrings.Length == 3)
                                                        capability.NumChannels = uint.Parse(codecInfoStrings[2]);
                                                }
                                            }

                                        }
                                        else if (attrName.Equals("rtcp-fb"))
                                        {
                                            //rtcp-fb:100 ccm goog-remb
                                            string[] caodecInfoStrings = attrValue.Split(' ');
                                            string codecId = caodecInfoStrings[0];

                                            RTCRtpCodecCapability capability = null;
                                            if (codecsDictionary.TryGetValue(codecId, out capability))
                                            {
                                                if (null == capability.RtcpFeedback)
                                                    capability.RtcpFeedback = new List<RTCRtcpFeedback>();

                                                RTCRtcpFeedback fb = new RTCRtcpFeedback();
                                                fb.Type = caodecInfoStrings[1];
                                                fb.Parameter = caodecInfoStrings.Length == 3 ? caodecInfoStrings[2] : "";
                                                capability.RtcpFeedback.Add(fb);
                                            }
                                            
                                        }
                                        else if (attrName.Equals("fmtp"))
                                        {

                                        }
                                        else if (attrName.Equals("maxptime"))
                                        {
                                            uint maxptime = uint.Parse(attrValue);
                                            foreach (var capability in codecsDictionary.Values)
                                            {
                                                capability.Maxptime = maxptime;
                                            }
                                        }
                                        else if (attrName.Equals("ssrc-group"))
                                        {
                                            string[] ssrcStrings = attrValue.Split(' ');
                                            foreach (var str in ssrcStrings)
                                            {
                                                if (!str.Equals("FID"))
                                                {
                                                    streamSourceIds.Add(uint.Parse(str));
                                                }
                                            }
                                        }
                                        else if (attrName.Equals("ssrc"))
                                        {
                                            //a=ssrc:1928739697 cname:0SagpZSf2NzVtTYz
                                            sep = attrValue.IndexOf(' ');

                                            if (sep == -1)
                                                break;
                                            string ssrcId = attrValue.Substring(0, sep);

                                            string ssrcString = attrValue.Substring(sep+1);
                                            string[] ssrcStrings = ssrcString.Split(':');

                                            string ssrcType = ssrcStrings[0];
                                            if (ssrcType.Equals("cname"))
                                            {
                                                streamName = ssrcStrings[1];
                                            }
                                            else if (ssrcType.Equals("msid"))
                                            {

                                            }
                                            else if (ssrcType.Equals("mslabel"))
                                            {
                                                streamId = ssrcStrings[1];
                                            }
                                            else if (ssrcType.Equals("label"))
                                            {
                                                mediaTrackLabel = ssrcStrings[1];
                                            }
                                        }
                                        break;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            valid = false;
                            //throw new Exception(string.Format("Invalid Line {0}", line));
                        }
                        line = reader.ReadLine();
                    }
                    return valid;
                }

                /*  internal static void parseSDP(string sdp)
          {
              string sessionId = null;
              string sessionVersion = null;
              string streamId = null;

              bool audioSupported = false;
              bool videoSupported = false;

              string ice_ufrag = null;
              string ice_pwd = null;

              string fingerprint = null;
              List<Dictionary<string,string>> extmaps = new List<Dictionary<string, string>>();

              List<string> codecs = new List<string>();
              if (string.IsNullOrEmpty(sdp))
                  return;

              TextReader reader = new StringReader(sdp);

              string line = reader.ReadLine();
              while (line != null)
              {
                  if ((line.Length == 0))
                  {
                      line = reader.ReadLine();
                      continue;
                  }
                  string value = line.Substring(2);
                  string[] parts = null;
                  int sep = -1;
                  try
                  {
                      switch (line[0])
                      {
                          case 'v':
                              int version = 0;
                              if (!int.TryParse(value, out version))
                              {
                                  throw new Exception(string.Format("Invalid Line {0}", line));
                              }
                              if (version != 0)
                              {
                                  throw new Exception(string.Format("Not supported version"));
                              }
                              break;

                          case 'o':
                              parts = value.Split(' ');
                              if (parts.Length != 6)
                              {
                                  throw new Exception(string.Format("Invalid Line {0}", line));
                              }
                              sessionId = parts[1];
                              sessionVersion = parts[2];
                              break;
                          case 's':
                              break;
                          case 't':
                              break;

                          case 'c':
                              parts = value.Split(' ');
                              if (parts.Length != 3)
                              {
                                  throw new Exception(string.Format("Invalid Line {0}", line));
                              }
                              string networkType = parts[0];
                              string addressType = parts[1];
                              parts = parts[2].Split('/');
                              if (parts.Length > 3)
                              {
                                  throw new Exception(string.Format("Invalid Line {0}", line));
                              }
                              //TODO: Check if this is required
                              break;
                          case 'a':
                              sep = value.IndexOf(':');
                              string attrName;
                              string attrValue;

                              if (sep != -1)
                                  break;

                              attrName = value.Substring(0, sep);
                              attrValue = value.Substring(sep + 1);

                              if (attrName.Equals("candidate"))
                              {
                                  //org.ortc.RTCIceCandidate iceCandidate;
                                  //iceCandidate = iceCandidateFromSdp(attrValue);
                              }
                              else if (attrName.Equals("group"))
                              {
                                  audioSupported = attrValue.Contains("audio");
                                  videoSupported = attrValue.Contains("video");
                              }
                              else if (attrName.Equals("msid-semantic"))
                              {
                                  streamId = attrValue;
                              }
                              else if (attrName.Equals("rtcp"))
                              {

                              }
                              else if (attrName.Equals("ice-ufrag"))
                              {
                                  ice_ufrag = attrValue;
                              }
                              else if (attrName.Equals("ice-pwd"))
                              {
                                  ice_pwd = attrValue;
                              }
                              else if (attrName.Equals("fingerprint"))
                              {
                                  fingerprint = attrValue;
                              }
                              else if (attrName.Equals("setup"))
                              {

                              }
                              else if (attrName.Equals("mid"))
                              {

                              }
                              else if (attrName.Equals("extmap"))
                              {
                                  sep = value.IndexOf(' ');

                                  if (sep != -1)
                                      break;

                                  string extensionPreferredId = value.Substring(0, sep);
                                  string extensionUri = value.Substring(sep + 1);
                                  Dictionary<string, string> extension = new Dictionary<string, string>();
                                  extension.Add(extensionPreferredId, extensionUri);
                                  extmaps.Add(extension);
                              }
                              else if (attrName.Equals("sendrecv"))
                              {

                              }
                              else if (attrName.Equals("rtcp-mux"))
                              {

                              }
                              else if (attrName.Equals("rtpmap"))
                              {

                              }
                              else if (attrName.Equals("rtcp-fb"))
                              {

                              }
                              else if (attrName.Equals("fmtp"))
                              {

                              }
                              else if (attrName.Equals("maxptime"))
                              {

                              }
                              else if (attrName.Equals("ssrc"))
                              {

                              }

                              break;
                          case 'm':
                              parts = value.Split(' ');
                              if (parts.Length < 4)
                              {
                                  break;
                              }

                              string mediaType = parts[0];
                              string port = parts[1];
                              string protocol = parts[2];
                              // var formats = parts.Skip(3).ToList();

                              break;
                      }
                  }
                  catch
                  {
                      throw new Exception(string.Format("Invalid Line {0}", line));
                  }
                  line = reader.ReadLine();
              }
          }*/
            }
        }
    }
}