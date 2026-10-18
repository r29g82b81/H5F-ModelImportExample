using Assimp;
using TagFramework.Src.TagFiles;
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
            //convert_model_to_tag("C:\\Users\\ct770\\Downloads\\tag testing\\Untitled.obj",
            //                     "C:\\Users\\ct770\\Downloads\\tag testing\\merged\\tripodmesh_og",
            //                     "C:\\Users\\ct770\\Downloads\\tag testing\\merged\\tripodmesh_og_res_0",
            //                     false,
            //                     "C:\\Users\\ct770\\Downloads\\tag testing\\result_tag");

            convert_model_to_tag("C:\\Users\\ct770\\Downloads\\tag testing\\h1magnum\\UNSC Weapons2.fbx",
                     "C:\\Users\\ct770\\Downloads\\tag testing\\h1magnum\\h1magnum",
                     "C:\\Users\\ct770\\Downloads\\tag testing\\h1magnum\\h1magnum_res_0",
                     true,
                     "C:\\Users\\ct770\\Downloads\\tag testing\\h1magnum\\result_magnum_tag");
        }

        static Tag load_tag(string file, string res_template)
        {
            if (!File.Exists(file)) throw new Exception("file does not exist");
            if (!File.Exists(res_template)) throw new Exception("res_template does not exist");
            byte[] file_bytes = File.ReadAllBytes(file);
            byte[] res_bytes = File.ReadAllBytes(res_template);

            var testing = new List<KeyValuePair<byte[], bool>>();
            testing.Add(new (res_bytes, true));
            Tag test = new Tag(testing); // apparently we do NOT support null, despite declaring it as nullable (this is for compiling at least)
            if (!test.Load_tag_file(file_bytes)) throw new Exception("failed to load tag");
            return test;
        }
        static void convert_model_to_tag(string input_model_path, string input_template_tag, string input_res_tag, bool has_bone_weighting, string output_tag_path){
            // load the template tag
            Tag rtgo_tag = load_tag(input_template_tag, input_res_tag);
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



                var vertexWeightCounts = new int[mesh.VertexCount];
                // Validate bone weights
                if (has_bone_weighting)
                {
                    for (int j = 0; j < mesh.Bones.Count; j++)
                    {
                        var bone = mesh.Bones[j];
                        foreach (var weight in bone.VertexWeights)
                        {
                            if (weight.VertexID < 0 || weight.VertexID >= mesh.VertexCount)
                                throw new Exception($"bone '{bone.Name}' references invalid vertex {weight.VertexID}");

                            vertexWeightCounts[weight.VertexID] = j;
                        }
                    }

                }



                // node remapping

                // 0 is good
                // 2 is good
                // whatever points to 04 should be 03 ? infact i believe these 2 need to be swapped around
                for (int j = 0;  j < vertexWeightCounts.Length; j++){
                    if (vertexWeightCounts[j] == 1) vertexWeightCounts[j] = 3;
                    else if (vertexWeightCounts[j] == 3) vertexWeightCounts[j] = 4;
                    else if (vertexWeightCounts[j] == 4) vertexWeightCounts[j] = 1;
                }

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
                foreach(var v in mesh.Vertices){
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

                float scaling_factor = 0.37f;

                rtgo_tag.set_float3("render geometry*\\\\compression info*\\0\\position bounds 0*", new(bound_x_minus * scaling_factor, bound_x * scaling_factor, bound_y_minus * scaling_factor));
                rtgo_tag.set_float3("render geometry*\\\\compression info*\\0\\position bounds 1*", new(bound_y * scaling_factor, bound_z_minus * scaling_factor, bound_z * scaling_factor));

                // preprocess UV bounds
                float uv_bound_x = 0.0f;
                float uv_bound_x_minus = 0.0f;
                float uv_bound_y = 0.0f;
                float uv_bound_y_minus = 0.0f;
                // most amount of UV channels is 3 i believe // even though we're just going to support the first channel
                if (mesh.TextureCoordinateChannelCount > 3) throw new Exception("exceeded maximum supported UV channels count! we can still process the first 3 however");
                foreach (var v in mesh.TextureCoordinateChannels[0]){
                    if (v.X > uv_bound_x)       uv_bound_x = v.X;
                    if (v.X < uv_bound_x_minus) uv_bound_x_minus = v.X;
                    if (v.Y > uv_bound_y)       uv_bound_y = v.Y;
                    if (v.Y < uv_bound_y_minus) uv_bound_y_minus = v.Y;
                }

                rtgo_tag.set_float2("render geometry*\\\\compression info*\\0\\texcoord bounds 0*", new(uv_bound_x_minus, uv_bound_x));
                rtgo_tag.set_float2("render geometry*\\\\compression info*\\0\\texcoord bounds 1*", new(uv_bound_y, uv_bound_y_minus));

                // now we need to start compiling the tag
                // open stream writer to start writing out the chunk data

                using (var stream = new MemoryStream())
                using (var bw = new BinaryWriter(stream)){
                    for (int j = 0; j < mesh.Vertices.Count; j++){
                        var vert = mesh.Vertices[j];


                        ushort packedX = (ushort)(((vert.X - bound_x_minus) / (bound_x - bound_x_minus)) * 65535.0f);
                        ushort packedY = (ushort)(((vert.Y - bound_y_minus) / (bound_y - bound_y_minus)) * 65535.0f);
                        ushort packedZ = (ushort)(((vert.Z - bound_z_minus) / (bound_z - bound_z_minus)) * 65535.0f);
                        bw.Write(packedX);
                        bw.Write(packedY);
                        bw.Write(packedZ);
                        bw.Write(new byte[2] {0,0}); // pad out the last guy?

                        // UV0s (4 bytes wide)
                        if (!invalid_UV0){
                            var uv = mesh.TextureCoordinateChannels[0][j];
                            // pack into 16 bits each
                            ushort uv_packedX = (ushort)(((uv.X - uv_bound_x_minus) / (uv_bound_x - uv_bound_x_minus)) * 65535.0f);
                            ushort uv_packedY = (ushort)(((uv.Y - uv_bound_y_minus) / (uv_bound_y - uv_bound_y_minus)) * 65535.0f);
                            bw.Write(uv_packedX);
                            bw.Write(uv_packedY);
                        }
                        else bw.Write(0u);

                        // normals (4 bytes wide)
                        if (!invalid_normals){
                            var normal = mesh.Normals[j];
                            // we have to pack this all into a single int to write
                            // make sure the normals work how we think they work
                            normal.X = Math.Clamp(normal.X, -1.0f, 1.0f);
                            normal.Y = Math.Clamp(normal.Y, -1.0f, 1.0f);
                            normal.Z = Math.Clamp(normal.Z, -1.0f, 1.0f);

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
                        if (!invalid_tangents){
                            var tangent = mesh.Tangents[j];

                            // copy & pasted normals logic
                            tangent.X = Math.Clamp(tangent.X, -1.0f, 1.0f);
                            tangent.Y = Math.Clamp(tangent.Y, -1.0f, 1.0f);
                            tangent.Z = Math.Clamp(tangent.Z, -1.0f, 1.0f);

                            ushort tangent_packed_x = (ushort)(((tangent.X + 1.0f) / 2.0f) * 0x3FF);
                            ushort tangent_packed_y = (ushort)(((tangent.Y + 1.0f) / 2.0f) * 0x3FF);
                            ushort tangent_packed_z = (ushort)(((tangent.Z + 1.0f) / 2.0f) * 0x3FF);

                            uint packed = ((uint)tangent_packed_x & 0x3ff) | (((uint)tangent_packed_y & 0x3ff) << 10) | (((uint)tangent_packed_z & 0x3ff) << 20);
                            bw.Write(packed);
                        }
                        else bw.Write(0u);

                        if (has_bone_weighting){
                            byte index = (byte)vertexWeightCounts[j];
                            bw.Write(index | index << 8 | index << 16 | index << 24);
                        }

                        // bytecolors (4 bytes wide)
                        if (!invalid_colors){
                            var color = mesh.VertexColorChannels[0][j];
                            bw.Write((byte)(color.A * 255));
                            bw.Write((byte)(color.R * 255));
                            bw.Write((byte)(color.G * 255));
                            bw.Write((byte)(color.B * 255));
                        }
                        else bw.Write(4278190080u);





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

                Tag.compiled_tag output = rtgo_tag.compile();
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