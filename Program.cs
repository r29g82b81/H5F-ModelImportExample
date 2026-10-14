using Assimp;
using Infinite_module_test;
using static Infinite_module_test.tag_structs;
using static System.Net.Mime.MediaTypeNames;

namespace Infinite_model_importer
{
    internal class Program
    {


        // refer to https://github.com/Gravemind2401/Reclaimer/blob/master/Reclaimer.Blam/Resources/Halo5VertexBuffer.xml
        // for any inquiries regarding halo 5 buffer formats

        static void Main(string[] args) 
        {
            Console.WriteLine("Hello, World!");
            convert_model_to_tag("C:\\Users\\ct770\\Downloads\\tag testing\\Untitled.obj",
                                 "C:\\Users\\ct770\\Downloads\\tag testing\\merged\\tripodmesh_og",
                                 "C:\\Users\\ct770\\Downloads\\tag testing\\merged\\tripodmesh_og_res_0",
                                 0x0,
                                 "C:\\Users\\ct770\\Downloads\\tag testing\\result_tag");
        }

        static tag load_tag(string file, string res_template)
        {
            if (!File.Exists(file)) throw new Exception("file does not exist");
            if (!File.Exists(res_template)) throw new Exception("res_template does not exist");
            byte[] file_bytes = File.ReadAllBytes(file);
            byte[] res_bytes = File.ReadAllBytes(res_template);

            var testing = new List<KeyValuePair<byte[], bool>>();
            testing.Add(new (res_bytes, true));
            tag test = new tag(testing); // apparently we do NOT support null, despite declaring it as nullable (this is for compiling at least)
            if (!test.Load_tag_file(file_bytes)) throw new Exception("failed to load tag");
            return test;
        }
        static void convert_model_to_tag(string input_model_path, string input_template_tag, string input_res_tag, uint new_tagid, string output_tag_path){
            // load the template tag
            tag rtgo_tag = load_tag(input_template_tag, input_res_tag);
            //rtgo_tag.set_tagID(new_tagid); // we have to update the tagID, as we're going to have the template set to -1


            // load the 3d model
            Assimp.Scene model;
            Assimp.AssimpContext importer = new Assimp.AssimpContext();
            //importer.SetConfig(new Assimp.Configs.NormalSmoothingAngleConfig(66.0f));
            model = importer.ImportFile(input_model_path, 0); // Assimp.PostProcessPreset.TargetRealTimeMaximumQuality);

            // for now the ideal thing we should do here is just bunch all our stuff into a single mesh part
            // or actually, we're just going to operate on the first mesh, assuming its the one we actually want to import

            for (int i = 0; i < model.MeshCount; i++){
                if (i > 0) continue; // ONLY DOING THE FIRST MESH FOR NOW!!!

                var mesh = model.Meshes[i];
                var indices = mesh.GetIndices();

                // verify the states of each vertex type
                bool invalid_verts = (mesh.Vertices.Count == 0);
                bool invalid_UV0 = (mesh.TextureCoordinateChannels.Length == 0 || mesh.TextureCoordinateChannels[0].Count == 0);
                bool invalid_normals = (mesh.Normals.Count == 0);
                bool invalid_tangents = (mesh.Tangents.Count == 0);
                bool invalid_colors = (mesh.VertexColorChannels.Length == 0 || mesh.VertexColorChannels[0].Count == 0);

                if (invalid_verts) throw new Exception("why would this even be invalid");
                // error check to make sure all buffers have the exact same number of entries
                if (!invalid_verts && mesh.Vertices.Count != mesh.VertexCount)                   throw new Exception("bad vertex position count");
                if (!invalid_UV0 && mesh.TextureCoordinateChannels[0].Count != mesh.VertexCount) throw new Exception("bad vertex UV's count");
                if (!invalid_normals && mesh.Normals.Count != mesh.VertexCount)                  throw new Exception("bad vertex normals count");
                if (!invalid_tangents && mesh.Tangents.Count != mesh.VertexCount)                throw new Exception("bad vertex tangents count");
                if (!invalid_colors && mesh.VertexColorChannels[0].Count != mesh.VertexCount)    throw new Exception("bad vertex colors count");


                // copy over base mesh info
                rtgo_tag.set_number("render geometry*\\\\meshes*\\0\\LOD render data\\0\\parts*\\0\\index count*", indices.Length.ToString());
                rtgo_tag.set_number("render geometry*\\\\meshes*\\0\\LOD render data\\0\\parts*\\0\\budget vertex count", mesh.VertexCount.ToString());

                rtgo_tag.set_number("render geometry*\\\\meshes*\\0\\LOD render data\\0\\subparts*\\0\\index count*", indices.Length.ToString());
                rtgo_tag.set_number("render geometry*\\\\meshes*\\0\\LOD render data\\0\\subparts*\\0\\budget vertex count", mesh.VertexCount.ToString());


                // preprocess vertex positions to find largest distance so we can normalize
                // we should fill in the compression data bounds?
                // this may actually be wrong. // NOTE: halo uses x y & z around different orders than we do, so time to swap them around until they work
                float bound_x = 0.0f;
                float bound_x_minus = 0.0f;
                float bound_y = 0.0f;
                float bound_y_minus = 0.0f;
                float bound_z = 0.0f;
                float bound_z_minus = 0.0f;

                float highest_distance = 0.0f;
                foreach(var v in mesh.Vertices){
                    if (Math.Abs(v.X) > highest_distance) highest_distance = Math.Abs(v.X);
                    if (Math.Abs(v.Y) > highest_distance) highest_distance = Math.Abs(v.Y);
                    if (Math.Abs(v.Z) > highest_distance) highest_distance = Math.Abs(v.Z);

                    if (v.X > bound_x)       bound_x = v.X;
                    if (v.X < bound_x_minus) bound_x_minus = v.X;
                    if (v.Y > bound_y)       bound_y = v.Y;
                    if (v.Y < bound_y_minus) bound_y_minus = v.Y;
                    if (v.Z > bound_z)       bound_z = v.Z;
                    if (v.Z < bound_z_minus) bound_z_minus = v.Z;
                }
                // update compression & per part data
                //rtgo_tag.set_float3("Per Mesh Data[0].Bounds min", new(bound_x, bound_y, bound_z));
                //rtgo_tag.set_float3("Per Mesh Data[0].Bounds max", new(bound_x_minus, bound_y_minus, bound_z_minus));

                rtgo_tag.set_float3("render geometry*\\\\compression info*\\0\\position bounds 0*", new(bound_x_minus, bound_x, bound_y_minus));
                rtgo_tag.set_float3("render geometry*\\\\compression info*\\0\\position bounds 1*", new(bound_y, bound_z_minus, bound_z));

                // preprocess UV bounds
                float uv_bound_x = 0.0f;
                float uv_bound_x_minus = 0.0f;
                float uv_bound_y = 0.0f;
                float uv_bound_y_minus = 0.0f;
                float uv_bound_z = 0.0f;
                float uv_bound_z_minus = 0.0f;

                float highest_uv0_distance = 0.0f;
                // most amount of UV channels is 3 i believe // even though we're just going to support the first channel
                if (mesh.TextureCoordinateChannelCount > 3) throw new Exception("exceeded maximum supported UV channels count! we can still process the first 3 however");
                foreach (var v in mesh.TextureCoordinateChannels[0]){
                    if (Math.Abs(v.X) > highest_uv0_distance) highest_uv0_distance = Math.Abs(v.X);
                    if (Math.Abs(v.Y) > highest_uv0_distance) highest_uv0_distance = Math.Abs(v.Y);

                    if (v.X > uv_bound_x)       uv_bound_x = v.X;
                    if (v.X < uv_bound_x_minus) uv_bound_x_minus = v.X;
                    if (v.Y > uv_bound_y)       uv_bound_y = v.Y;
                    if (v.Y < uv_bound_y_minus) uv_bound_y_minus = v.Y;
                    //if (v.Z > uv_bound_z)       uv_bound_z = v.Z;
                    //if (v.Z < uv_bound_z_minus) uv_bound_z_minus = v.Z;
                }

                rtgo_tag.set_float2("render geometry*\\\\compression info*\\0\\texcoord bounds 0*", new(uv_bound_x, uv_bound_x_minus));
                rtgo_tag.set_float2("render geometry*\\\\compression info*\\0\\texcoord bounds 1*", new(uv_bound_y, uv_bound_y_minus));

                // now we need to start compiling the tag
                // open stream writer to start writing out the chunk data

                using (var stream = new MemoryStream())
                using (var bw = new BinaryWriter(stream)){
                    for (int j = 0; j < mesh.Vertices.Count; j++){
                        var vert = mesh.Vertices[j];


                        // start with vertex positions (8 bytes wide)
                        // normalize & pack each vector
                        float normalized_x = ((vert.X / highest_distance) + 1) / 2; 
                        ushort packed_x = (ushort)(normalized_x * 0xffff);
                        bw.Write(packed_x);
                        float unpacked_x = (((float)(ushort)packed_x / 0xffff) * 2.0f) - 1.0f;

                        float normalized_y = ((vert.Y / highest_distance) + 1) / 2;
                        ushort packed_y = (ushort)(normalized_y * 0xffff);
                        bw.Write(packed_y);
                        float unpacked_y = (((float)(ushort)packed_y / 0xffff) * 2.0f) - 1.0f;

                        float normalized_z = ((vert.Z / highest_distance) + 1) / 2;
                        ushort packed_z = (ushort)(normalized_z * 0xffff);
                        bw.Write(packed_z);
                        float unpacked_z = (((float)(ushort)packed_z / 0xffff) * 2.0f) - 1.0f;
                        bw.Write(new byte[2] {0,0}); // pad out the last guy?

                        // UV0s (4 bytes wide)
                        if (!invalid_UV0){
                            var uv = mesh.TextureCoordinateChannels[0][j];
                            // normalize & pack each vector // NOTE: we may have to prevent the endianness flipping when writing the bytes?
                            float uv_normalized_x = ((uv.X / highest_uv0_distance) + 1) / 2;
                            bw.Write((short)(uv_normalized_x * 0xffff));

                            float uv_normalized_y = ((uv.Y / highest_uv0_distance) + 1) / 2;
                            bw.Write((short)(uv_normalized_y * 0xffff));
                        }
                        else bw.Write(0u);

                        // normals (4 bytes wide)
                        if (!invalid_normals){
                            var normal = mesh.Normals[j];
                            // we have to pack this all into a single int to write
                            // make sure the normals work how we think they work
                            if (normal.X > 1.0f || normal.X < -1.0f || normal.Y > 1.0f || normal.Y < -1.0f || normal.Z > 1.0f || normal.Z < -1.0f) throw new Exception("out of bounds normal value?");
                            ushort normal_packed_x = (ushort)(((normal.X + 1.0f) / 2.0f) * 0x3FF); // uhh i think the conversion will be fine here
                            ushort normal_packed_y = (ushort)(((normal.Y + 1.0f) / 2.0f) * 0x3FF);
                            ushort normal_packed_z = (ushort)(((normal.Z + 1.0f) / 2.0f) * 0x3FF);

                            // this is how they're packed
                            // buffer[(i * 3)] = ((float)(block & 0x3ff) / 1023u - 0.5f) * 2;

                            uint packed = ((uint)normal_packed_x & 0x3ff) | (((uint)normal_packed_y & 0x3ff) << 10) | (((uint)normal_packed_z & 0x3ff) << 20);
                            bw.Write(packed);
                        }
                        else bw.Write(0u);

                        // tangents (4 bytes wide)
                        //if (!invalid_tangents){
                        //    var tangent = mesh.Tangents[j];
                        //    // make sure tangents work how we think they work
                        //    if (tangent.X > 1.0f || tangent.X < -1.0f || tangent.Y > 1.0f || tangent.Y < -1.0f || tangent.Z > 1.0f || tangent.Z < -1.0f) throw new Exception("out of bounds normal value?");
                        //    bw.Write((byte)(((tangent.X + 1.0f) / 2.0f) * 0xFF));
                        //    bw.Write((byte)(((tangent.Y + 1.0f) / 2.0f) * 0xFF));
                        //    bw.Write((byte)(((tangent.Z + 1.0f) / 2.0f) * 0xFF));
                        //}
                        //else 
                            bw.Write(0u);

                        // bytecolors (4 bytes wide)
                        //if (!invalid_colors) { 
                        //    var color = mesh.VertexColorChannels[0][j];
                        //    bw.Write((byte)(color.A * 255));
                        //    bw.Write((byte)(color.R * 255));
                        //    bw.Write((byte)(color.G * 255));
                        //    bw.Write((byte)(color.B * 255));
                        //}
                        //else 
                            bw.Write(4278190080u);

                    }
                    byte[] data = stream.ToArray();
                    rtgo_tag.set_number("render geometry*\\\\mesh package*!\\\\mesh resource groups*!\\0\\mesh resource!*\\\\pc vertex buffers*\\0\\count*", mesh.VertexCount.ToString());
                    rtgo_tag.set_number("render geometry*\\\\mesh package*!\\\\mesh resource groups*!\\0\\mesh resource!*\\\\pc vertex buffers*\\0\\d3dbuffer*\\\\byte width*", data.Length.ToString());
                    rtgo_tag.set_datablock("render geometry*\\\\mesh package*!\\\\mesh resource groups*!\\0\\mesh resource!*\\\\pc vertex buffers*\\0\\d3dbuffer*\\\\d3d buffer*", data);
                }


                using (var stream1 = new MemoryStream())
                using (var bw1 = new BinaryWriter(stream1))
                {
                    foreach (var v in indices)
                        bw1.Write((ushort)v);

                    byte[] data = stream1.ToArray();
                    rtgo_tag.set_number("render geometry*\\\\mesh package*!\\\\mesh resource groups*!\\0\\mesh resource!*\\\\pc index buffers*\\0\\count*", indices.Length.ToString());
                    rtgo_tag.set_number("render geometry*\\\\mesh package*!\\\\mesh resource groups*!\\0\\mesh resource!*\\\\pc index buffers*\\0\\d3dbuffer*\\\\byte width*", data.Length.ToString());
                    rtgo_tag.set_datablock("render geometry*\\\\mesh package*!\\\\mesh resource groups*!\\0\\mesh resource!*\\\\pc index buffers*\\0\\d3dbuffer*\\\\d3d buffer*", data);
                }

                tag.compiled_tag output = rtgo_tag.compile();
                // we aren't going to output resources with this, so we only need to worry about spitting out the main file
                File.WriteAllBytes(output_tag_path, output.tag_bytes);
                for (int j = 0; j < output.resource_bytes.Count; j++)
                {
                    File.WriteAllBytes(output_tag_path + "_res_" + j, output.resource_bytes[j]);
                }
            }

        }
    }
}