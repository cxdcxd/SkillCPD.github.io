﻿/*
© Siemens AG, 2017-2019
Author: Berkay Alp Cakal (berkay_alp.cakal.ct@siemens.com)

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
<http://www.apache.org/licenses/LICENSE-2.0>.
Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

// Added allocation free alternatives
// UoK , 2019, Odysseas Doumas (od79@kent.ac.uk / odydoum@gmail.com) 

using System;

namespace RosSharp.RosBridgeClient
{
    public static class HeaderExtensions
    {
        private static Timer timer = new Timer();

        public static void Update(this MessageTypes.Std.Header header)
        {
            //TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            //uint result = (uint)t.TotalMilliseconds;

            header.seq++;

            //header.stamp.secs = 5;
            //header.stamp.nsecs = 0;
            timer.Now(header.stamp);
        }
    }
}
