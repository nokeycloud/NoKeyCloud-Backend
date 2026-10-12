import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router";
import { useAuth } from "@/entities/session";
import { FileExplorer } from "@/widgets/file-explorer";
import { Button } from "@/shared/ui/button";
import {FolderPlus, Upload} from "lucide-react";
import {
    WindowsFolderOpen,
    WindowsCDROMDrive
} from "react-old-icons";
import { CreateFolderDialog } from "@/features/create-folder";
import { UploadFileDialog } from "@/features/upload-file";
import { useTheme } from "@/app/providers/ThemeProvider";

export function DashboardPage() {
    const navigate = useNavigate();
    const { folderId } = useParams();
    const auth = useAuth();
    const [refreshKey, setRefreshKey] = useState(0);
    const { theme } = useTheme();

    useEffect(() => {
        document.title = "noKeyCloud";
    }, []);

    useEffect(() => {
        if (auth.status === "guest") {
            navigate("/login", { replace: true });
        }
    }, [auth.status, navigate]);

    const rootFolderId = auth.rootFolderId ?? "";
    const currentFolderId = folderId || rootFolderId;

    if (auth.status !== "authenticated" || !currentFolderId) {
        return null;
    }

    return (
        <div className="flex-1 p-8 pt-4">
            <div className="flex flex-col sm:flex-row items-center justify-between mb-4">
                <div className="flex items-center gap-2">
                    <CreateFolderDialog
                        parentId={currentFolderId}
                        onSuccess={() => setRefreshKey((prev) => prev + 1)}
                    >
                        <Button>
                            {theme === "win69"
                                ? <WindowsFolderOpen size={24} />
                                : <FolderPlus className="h-4 w-4 mr-2" />
                            }

                            New Folder
                        </Button>
                    </CreateFolderDialog>
                    <UploadFileDialog
                        parentId={currentFolderId}
                        onSuccess={() => setRefreshKey((prev) => prev + 1)}
                    >
                        <Button variant="secondary">
                            {theme === "win69"
                                ? <WindowsCDROMDrive size={24} />
                                : <Upload className="h-4 w-4 mr-2" />
                            }

                            Upload File
                        </Button>
                    </UploadFileDialog>
                </div>
            </div>

            <FileExplorer
                key={refreshKey}
                folderId={currentFolderId}
                rootFolderId={rootFolderId}
            />
        </div>
    );
}